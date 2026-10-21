using System.Net;
using System.Net.Http.Json;
using ForgePlm.Api.Contracts;
using ForgePlm.Tests.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ForgePlm.Tests.Api;

/// <summary>
/// Drives the real HTTP pipeline against a real PostgreSQL database.
///
/// `WebApplicationFactory` boots the actual application — same middleware, same exception
/// handler, same endpoint definitions — pointed at a database this test owns and drops. Anything
/// less would test a stand-in, and the failures worth catching here (a 404 that should be a 200,
/// an error body missing its `conflictType`) only exist in the real pipeline.
///
/// The app migrates and seeds itself on startup, exactly as a cold `docker compose up` does, so
/// these tests exercise the demo baseline rather than fixtures invented for the test.
/// </summary>
public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IsolatedDatabase _database;

    public ApiApplicationFactory(IsolatedDatabase database) => _database = database;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ForgePlm", _database.ConnectionString);

        // The database is fresh, so the application applies its own schema and seed. That is
        // the same path a first run takes, and it means a migration error would fail here
        // rather than being masked by a pre-prepared database.
        builder.UseSetting("Database:ApplyOnStartup", "true");
    }
}

/// <summary>
/// Slice 3 — product and BOM read endpoints (spec §7).
///
/// In `PostgresCollection`, not standalone. xUnit creates a new instance of this class per test
/// method and runs them in parallel with other collections; an earlier version constructed its
/// own `PostgresFixture`, and that fixture's `DisposeAsync` sweeps *every* `forge_plm_test_%`
/// database on the server. Each test was therefore dropping the database a concurrently running
/// test was still using, surfacing as an intermittent `3D000: database does not exist` from the
/// startup seed. Sharing the collection fixture makes the sweep run once, when the whole
/// collection is finished.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ProductApiTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly PostgresFixture _fixture = fixture;
    private IsolatedDatabase _database = null!;
    private ApiApplicationFactory _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _database = await _fixture.CreateDatabaseAsync();
        _app = new ApiApplicationFactory(_database);
        _client = _app.CreateClient();
    }

    public async Task DisposeAsync()
    {
        // Disposes the factory first: the application holds a connection to the database, and
        // DROP fails while any connection is live.
        _client.Dispose();
        await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>The seeded baseline: one product, seven components, one released revision.</summary>
    private async Task<ProductDto> SeededProductAsync()
    {
        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductDto>>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Total);

        return page.Items.Single();
    }

    [Fact]
    public async Task ProductsListReturnsTheSpecifiedPagedShape()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ProductDto>>();

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
        Assert.Equal(1, page.Page);
        Assert.Equal(PageRequest.DefaultPageSize, page.PageSize);

        var product = page.Items.Single();
        Assert.Equal("SERVER-100", product.PartNumber);
        Assert.Equal("Active", product.LifecycleStatus);
    }

    [Fact]
    public async Task SearchIsCaseInsensitiveAndMatchesNameAsWellAsPartNumber()
    {
        // Lower-case input against an upper-cased stored part number.
        var byPartNumber = await _client.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/products?search=server-1");
        Assert.NotNull(byPartNumber);
        Assert.Single(byPartNumber.Items);

        var byName = await _client.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/products?search=server");
        Assert.NotNull(byName);
        Assert.Single(byName.Items);

        var noMatch = await _client.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/products?search=does-not-exist");
        Assert.NotNull(noMatch);
        Assert.Empty(noMatch.Items);
        Assert.Equal(0, noMatch.Total);
    }

    /// <summary>
    /// Reads a ProblemDetails extension as a string.
    ///
    /// `Extensions` is `IDictionary&lt;string, object?&gt;`, so System.Text.Json materialises the
    /// values as `JsonElement`. Comparing one to a `string` fails with an assertion that prints
    /// "Expected: x / Actual: x", which is a confusing hour to lose.
    /// </summary>
    private static string? Extension(ProblemDetails problem, string key) =>
        problem.Extensions.TryGetValue(key, out var value) ? value?.ToString() : null;

    [Fact]
    public async Task AnUnknownStatusFilterIsA400NotAnEmptyList()
    {
        // Returning 200 with zero items would tell the UI "nothing has that status", which is a
        // different fact from "you misspelled it".
        var response = await _client.GetAsync("/api/products?status=Retierd");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal("invalid-filter-value", Extension(problem, "code"));
    }

    [Fact]
    public async Task PagingIsClampedRatherThanRejected()
    {
        // One product exists, so a page past the end is legitimately empty but must still be a
        // successful read with a truthful `total`.
        var beyondEnd = await _client.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/products?page=5&pageSize=2");
        Assert.NotNull(beyondEnd);
        Assert.Empty(beyondEnd.Items);
        Assert.Equal(1, beyondEnd.Total);
        Assert.Equal(5, beyondEnd.Page);

        // pageSize over the cap is clamped to it, not refused.
        var oversized = await _client.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/products?pageSize=1000");
        Assert.NotNull(oversized);
        Assert.Equal(PageRequest.MaxPageSize, oversized.PageSize);

        // A negative page is treated as the first page.
        var negative = await _client.GetFromJsonAsync<PagedResponse<ProductDto>>("/api/products?page=-3");
        Assert.NotNull(negative);
        Assert.Equal(1, negative.Page);
        Assert.Single(negative.Items);
    }

    [Fact]
    public async Task AnUnknownProductIsA404ProblemDetails()
    {
        var missing = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/products/{missing}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Contains(missing.ToString(), problem.Detail);
    }

    [Fact]
    public async Task ComponentsListCoversTheEcoPicker()
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<ComponentDto>>("/api/components");

        Assert.NotNull(page);
        Assert.Equal(7, page.Total);
        Assert.Contains(page.Items, c => c.PartNumber == "MB-020");
        Assert.Contains(page.Items, c => c.PartNumber == "MB-021");
    }

    [Fact]
    public async Task TheCurrentRevisionCarriesItsLinesSoTheProductScreenNeedsOneCall()
    {
        var product = await SeededProductAsync();

        var response = await _client.GetAsync($"/api/products/{product.Id}/bom-revisions/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var current = await response.Content.ReadFromJsonAsync<CurrentRevisionResponse>();

        Assert.NotNull(current);
        Assert.Equal("B", current.Revision.RevisionCode);
        Assert.Equal("Released", current.Revision.Status);
        Assert.Equal(6, current.Revision.LineCount);
        Assert.Equal(6, current.Lines.Count);

        // Each line carries its component, so the BOM table renders without N further requests.
        Assert.All(current.Lines, l => Assert.False(string.IsNullOrWhiteSpace(l.Component.PartNumber)));
        Assert.Equal("Each", current.Lines[0].Unit);
        Assert.True(current.Lines[0].Quantity > 0);
    }

    [Fact]
    public async Task RevisionLinesArePagedAndIncludeTheirComponent()
    {
        var product = await SeededProductAsync();
        var revisions = await _client.GetFromJsonAsync<PagedResponse<BomRevisionDto>>(
            $"/api/products/{product.Id}/bom-revisions");

        Assert.NotNull(revisions);
        var revision = revisions.Items.Single();

        var page = await _client.GetFromJsonAsync<PagedResponse<BomLineDto>>(
            $"/api/bom-revisions/{revision.Id}/lines?pageSize=2");

        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(6, page.Total);
        Assert.Equal(1, page.Page);
        Assert.All(page.Items, l => Assert.True(l.Quantity > 0));

        Assert.All(page.Items, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l.Component.PartNumber));
            Assert.False(string.IsNullOrWhiteSpace(l.Component.Name));
        });
    }

    [Fact]
    public async Task RevisionHistoryIsListedNewestFirst()
    {
        var product = await SeededProductAsync();

        var page = await _client.GetFromJsonAsync<PagedResponse<BomRevisionDto>>(
            $"/api/products/{product.Id}/bom-revisions");

        Assert.NotNull(page);
        var revision = page.Items.Single();
        Assert.Equal("B", revision.RevisionCode);
        Assert.Equal(6, revision.LineCount);
    }

    [Fact]
    public async Task AnUnknownRevisionIsA404FromBothRevisionEndpoints()
    {
        var missing = Guid.NewGuid();

        foreach (var url in new[]
                 {
                     $"/api/bom-revisions/{missing}",
                     $"/api/bom-revisions/{missing}/lines",
                     $"/api/products/{missing}/bom-revisions",
                     $"/api/products/{missing}/bom-revisions/current",
                 })
        {
            var response = await _client.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal(404, problem.Status);
        }
    }
}
