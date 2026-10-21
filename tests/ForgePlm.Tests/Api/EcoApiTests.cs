using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgePlm.Api.Contracts;
using ForgePlm.Api.Seed;
using ForgePlm.Domain.EngineeringChanges;
using ForgePlm.Domain.Products;
using ForgePlm.Tests.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ForgePlm.Tests.Api;

/// <summary>
/// Slices 4–6 through the real HTTP pipeline (spec §7, `docs/ECO-API-CONTRACT.md`).
///
/// The point of these tests is the contract as the frontend sees it: status codes, DTO shape,
/// string enums, and the error codes. The use-case semantics — transactions, concurrency and
/// rollback — are proved against PostgreSQL in <see cref="Integration.EcoWorkflowTests"/>.
/// </summary>
[Collection(PostgresCollection.Name)]
public class EcoApiTests(PostgresFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The API deliberately accepts only string enum values (`allowIntegerValues: false`).
    /// The test client has to opt into that too, or `PostAsJsonAsync` would send `0`.
    /// </summary>
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

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
        _client.Dispose();
        await _app.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static EcoLineRequest ReplaceMotherboardLine() =>
        new(EngineeringChangeType.ReplaceComponent, DemoSeed.Mb020Id, DemoSeed.Mb021Id, null, "Controller spec update");

    private static CreateEcoRequest CreateRequest(
        string title = "Replace motherboard controller",
        string? actor = "Alex Morgan",
        IReadOnlyList<EcoLineRequest>? lines = null) =>
        new(
            DemoSeed.Server100Id,
            title,
            "Updated controller specification and improved component availability.",
            "Existing released revision B remains available for historical reference.",
            null,
            actor,
            lines ?? [ReplaceMotherboardLine()]);

    private async Task<HttpResponseMessage> PostJsonAsync<T>(string url, T body) =>
        await _client.PostAsJsonAsync(url, body, ApiJson);

    private async Task<EcoDetailDto> CreateEcoAsync(string title = "Replace motherboard controller")
    {
        var response = await PostJsonAsync("/api/ecos", CreateRequest(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<EcoDetailDto>();
        Assert.NotNull(detail);
        return detail;
    }

    private async Task<EcoDetailDto> PostActionAsync(string url, object body)
    {
        var response = await PostJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<EcoDetailDto>();
        Assert.NotNull(detail);
        return detail;
    }

    private static async Task<ProblemDetails> ProblemAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        return problem;
    }

    private static string? Extension(ProblemDetails problem, string key) =>
        problem.Extensions.TryGetValue(key, out var value) ? value?.ToString() : null;

    private async Task<CurrentRevisionResponse> CurrentRevisionAsync()
    {
        var current = await _client.GetFromJsonAsync<CurrentRevisionResponse>(
            $"/api/products/{DemoSeed.Server100Id}/bom-revisions/current");

        Assert.NotNull(current);
        return current;
    }

    /// <summary>
    /// D-02c's protected path, end to end through HTTP:
    /// products → SERVER-100 rev B → create ECO (MB-020 → MB-021) → submit → approve → release
    /// → audit timeline.
    /// </summary>
    [Fact]
    public async Task TheProtectedDemoPathWorksEndToEnd()
    {
        var before = await CurrentRevisionAsync();
        Assert.Equal("B", before.Revision.RevisionCode);
        Assert.Contains(before.Lines, line => line.Component.PartNumber == "MB-020");

        var createResponse = await PostJsonAsync("/api/ecos", CreateRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var location = createResponse.Headers.Location;
        Assert.NotNull(location);

        var created = await createResponse.Content.ReadFromJsonAsync<EcoDetailDto>();
        Assert.NotNull(created);

        Assert.Matches(@"^ECO-\d{4}-\d{4}$", created.EcoNumber);
        Assert.Equal("Draft", created.Status);
        Assert.Equal(DemoSeed.RevBId, created.BaseReleasedRevisionId);
        Assert.Equal("B", created.BaseRevisionCode);
        Assert.Equal("SERVER-100", created.Product.PartNumber);
        Assert.Equal(created.Id.ToString(), location!.ToString()!.Split('/').Last());
        Assert.Single(created.Lines);
        Assert.Equal("ReplaceComponent", created.Lines[0].ChangeType);
        Assert.Equal("MB-020", created.Lines[0].Target.PartNumber);
        Assert.Equal("MB-021", created.Lines[0].Replacement!.PartNumber);
        Assert.Equal(1, created.LineCount);

        var submitted = await PostActionAsync($"/api/ecos/{created.Id}/submit", new ActorRequest("Alex Morgan"));
        Assert.Equal("Submitted", submitted.Status);
        Assert.NotNull(submitted.SubmittedAtUtc);

        var approved = await PostActionAsync(
            $"/api/ecos/{created.Id}/approve",
            new ApproveEcoRequest("Jordan Lee", "Validated on test bench."));

        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.ApprovedRevisionId);
        Assert.Equal("Jordan Lee", approved.DecidedBy);

        // ADR-003: approval is not release. The product still points at revision B.
        var afterApproval = await CurrentRevisionAsync();
        Assert.Equal("B", afterApproval.Revision.RevisionCode);
        Assert.Equal("Released", afterApproval.Revision.Status);

        var released = await PostActionAsync(
            $"/api/ecos/{created.Id}/release",
            new ReleaseEcoRequest("Jordan Lee", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("Applied", released.Status);

        var afterRelease = await CurrentRevisionAsync();
        Assert.Equal("C", afterRelease.Revision.RevisionCode);
        Assert.Equal("Released", afterRelease.Revision.Status);
        Assert.NotNull(afterRelease.Revision.EffectiveAtUtc);
        Assert.Contains(afterRelease.Lines, line => line.Component.PartNumber == "MB-021" && line.Quantity == 1);
        Assert.DoesNotContain(afterRelease.Lines, line => line.Component.PartNumber == "MB-020");

        var revisions = await _client.GetFromJsonAsync<PagedResponse<BomRevisionDto>>(
            $"/api/products/{DemoSeed.Server100Id}/bom-revisions");

        Assert.NotNull(revisions);
        Assert.Contains(revisions.Items, revision => revision.RevisionCode == "B" && revision.Status == "Superseded");
        Assert.Contains(revisions.Items, revision => revision.RevisionCode == "C" && revision.Status == "Released");

        var audit = await _client.GetFromJsonAsync<PagedResponse<AuditEventDto>>(
            $"/api/audit?entityType=EngineeringChangeOrder&entityId={created.Id}");

        Assert.NotNull(audit);
        Assert.Equal(4, audit.Total);
        Assert.Equal(
            new[] { "RevisionReleased", "EcoApproved", "EcoSubmitted", "EcoCreated" },
            audit.Items.Select(item => item.Action));
        Assert.All(audit.Items, item => Assert.Equal(JsonValueKind.Object, item.Details.ValueKind));
        Assert.Equal("Jordan Lee", audit.Items[0].Actor);
    }

    /// <summary>
    /// IT-01 by construction: a client cannot pick the base revision or the ECO number, because
    /// neither is part of the contract. Unknown JSON members are ignored, so a stale client that
    /// sends them changes nothing.
    /// </summary>
    [Fact]
    public async Task AStaleClientCannotChooseTheBaseRevisionOrTheEcoNumber()
    {
        var payload = new
        {
            productId = DemoSeed.Server100Id,
            baseReleasedRevisionId = Guid.NewGuid(),
            ecoNumber = "ECO-1999-9999",
            title = "Attempt to rebase from the client",
            reasonForChange = "Should be ignored.",
            actor = "Alex Morgan",
            lines = new[]
            {
                new
                {
                    changeType = "ReplaceComponent",
                    targetComponentId = DemoSeed.Mb020Id,
                    replacementComponentId = DemoSeed.Mb021Id,
                },
            },
        };

        var response = await PostJsonAsync("/api/ecos", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<EcoDetailDto>();
        Assert.NotNull(created);

        Assert.Equal(DemoSeed.RevBId, created.BaseReleasedRevisionId);
        Assert.NotEqual("ECO-1999-9999", created.EcoNumber);
        Assert.Matches(@"^ECO-\d{4}-\d{4}$", created.EcoNumber);
    }

    /// <summary>A product with no released BOM has its own conflict code, not a 500.</summary>
    [Fact]
    public async Task AProductWithoutAReleasedRevisionIsA409NoReleasedRevision()
    {
        var product = Product.Create(
            "NOBASE-1", "Product without a BOM", "Server", ProductLifecycleStatus.Active, DateTime.UtcNow);

        await using (var db = _database.CreateDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var response = await PostJsonAsync("/api/ecos", CreateRequest() with { ProductId = product.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await ProblemAsync(response);
        Assert.Equal("no-released-revision", Extension(problem, "code"));
        Assert.Equal("no-released-revision", Extension(problem, "conflictType"));
    }

    /// <summary>Spec §7: a draft may be edited; any other status is `invalid-transition`.</summary>
    [Fact]
    public async Task ADraftCanBeEditedAndASubmittedEcoCannot()
    {
        var created = await CreateEcoAsync("Original title");

        var update = new UpdateEcoRequest(
            "Edited title",
            "A revised reason.",
            "A revised impact assessment.",
            new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            [new EcoLineRequest(EngineeringChangeType.ChangeQuantity, DemoSeed.Ram032Id, null, 16, "Double the memory")]);

        var put = await _client.PutAsJsonAsync($"/api/ecos/{created.Id}", update, ApiJson);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var updated = await put.Content.ReadFromJsonAsync<EcoDetailDto>();
        Assert.NotNull(updated);
        Assert.Equal("Edited title", updated.Title);
        Assert.Equal("A revised reason.", updated.ReasonForChange);
        Assert.Equal(16, updated.Lines.Single().NewQuantity);
        Assert.Equal("ChangeQuantity", updated.Lines.Single().ChangeType);

        await PostActionAsync($"/api/ecos/{created.Id}/submit", new ActorRequest("Alex Morgan"));

        var blocked = await _client.PutAsJsonAsync($"/api/ecos/{created.Id}", update, ApiJson);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var problem = await ProblemAsync(blocked);
        Assert.Equal("invalid-transition", Extension(problem, "conflictType"));
    }

    /// <summary>DT-06 at the API boundary: a blank reason is 400 and creates no revision.</summary>
    [Fact]
    public async Task RejectionRequiresAReasonAndCreatesNoRevision()
    {
        var created = await CreateEcoAsync();
        await PostActionAsync($"/api/ecos/{created.Id}/submit", new ActorRequest("Alex Morgan"));

        var blank = await PostJsonAsync(
            $"/api/ecos/{created.Id}/reject",
            new RejectEcoRequest("Jordan Lee", "   "));

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal("rejection-reason-required", Extension(await ProblemAsync(blank), "code"));

        var rejected = await PostActionAsync(
            $"/api/ecos/{created.Id}/reject",
            new RejectEcoRequest("Jordan Lee", "Alternative component already qualified."));

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Alternative component already qualified.", rejected.ReviewerComment);

        var revisions = await _client.GetFromJsonAsync<PagedResponse<BomRevisionDto>>(
            $"/api/products/{DemoSeed.Server100Id}/bom-revisions");
        Assert.NotNull(revisions);
        Assert.Equal(1, revisions.Total);

        // A rejected ECO cannot be approved afterwards.
        var approve = await PostJsonAsync(
            $"/api/ecos/{created.Id}/approve",
            new ApproveEcoRequest("Jordan Lee", null));

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Equal("invalid-transition", Extension(await ProblemAsync(approve), "conflictType"));
    }

    /// <summary>
    /// D-04b: release requires a date, and a future date is recorded while taking effect
    /// immediately. The missing-date case is a 400 and leaves the ECO approved and unreleased.
    /// </summary>
    [Fact]
    public async Task ReleaseRequiresAnEffectiveDate()
    {
        var created = await CreateEcoAsync();
        await PostActionAsync($"/api/ecos/{created.Id}/submit", new ActorRequest("Alex Morgan"));
        await PostActionAsync($"/api/ecos/{created.Id}/approve", new ApproveEcoRequest("Jordan Lee", null));

        var response = await PostJsonAsync(
            $"/api/ecos/{created.Id}/release",
            new ReleaseEcoRequest("Jordan Lee", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("effective-date-required", Extension(await ProblemAsync(response), "code"));

        var current = await CurrentRevisionAsync();
        Assert.Equal("B", current.Revision.RevisionCode);
    }

    /// <summary>Spec §7 filters, and a misspelled status is a 400 rather than an empty list.</summary>
    [Fact]
    public async Task EcoListSupportsSearchStatusAndProductFilters()
    {
        var alpha = await CreateEcoAsync("Alpha change");
        await CreateEcoAsync("Beta change");
        await PostActionAsync($"/api/ecos/{alpha.Id}/submit", new ActorRequest("Alex Morgan"));

        var all = await _client.GetFromJsonAsync<PagedResponse<EcoSummaryDto>>("/api/ecos");
        Assert.NotNull(all);
        Assert.Equal(2, all.Total);

        var submitted = await _client.GetFromJsonAsync<PagedResponse<EcoSummaryDto>>("/api/ecos?status=Submitted");
        Assert.NotNull(submitted);
        Assert.Single(submitted.Items);
        Assert.Equal(alpha.Id, submitted.Items[0].Id);

        var drafts = await _client.GetFromJsonAsync<PagedResponse<EcoSummaryDto>>("/api/ecos?status=draft");
        Assert.NotNull(drafts);
        Assert.Equal(1, drafts.Total);

        var searched = await _client.GetFromJsonAsync<PagedResponse<EcoSummaryDto>>("/api/ecos?search=alpha");
        Assert.NotNull(searched);
        Assert.Equal(alpha.Id, searched.Items.Single().Id);

        var byNumber = await _client.GetFromJsonAsync<PagedResponse<EcoSummaryDto>>(
            $"/api/ecos?search={alpha.EcoNumber}");
        Assert.NotNull(byNumber);
        Assert.Equal(alpha.Id, byNumber.Items.Single().Id);

        var byProduct = await _client.GetFromJsonAsync<PagedResponse<EcoSummaryDto>>(
            $"/api/ecos?productId={DemoSeed.Server100Id}");
        Assert.NotNull(byProduct);
        Assert.Equal(2, byProduct.Total);

        var invalid = await _client.GetAsync("/api/ecos?status=Retierd");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("invalid-filter-value", Extension(await ProblemAsync(invalid), "code"));
    }

    /// <summary>IT-06 through HTTP: both ECOs are approved, but only one can become current.</summary>
    [Fact]
    public async Task TwoCompetingEcosCannotBothRelease()
    {
        var first = await CreateEcoAsync("First competing change");
        var second = await CreateEcoAsync("Second competing change");

        foreach (var eco in new[] { first, second })
        {
            await PostActionAsync($"/api/ecos/{eco.Id}/submit", new ActorRequest("Alex Morgan"));
            await PostActionAsync($"/api/ecos/{eco.Id}/approve", new ApproveEcoRequest("Jordan Lee", null));
        }

        await PostActionAsync(
            $"/api/ecos/{first.Id}/release",
            new ReleaseEcoRequest("Jordan Lee", DateTime.UtcNow));

        var losing = await PostJsonAsync(
            $"/api/ecos/{second.Id}/release",
            new ReleaseEcoRequest("Jordan Lee", DateTime.UtcNow));

        Assert.Equal(HttpStatusCode.Conflict, losing.StatusCode);
        Assert.Equal("stale-base-revision", Extension(await ProblemAsync(losing), "conflictType"));

        var current = await CurrentRevisionAsync();
        Assert.Equal("C", current.Revision.RevisionCode);
        Assert.Equal("Released", current.Revision.Status);

        var applied = await _client.GetFromJsonAsync<PagedResponse<EcoSummaryDto>>("/api/ecos?status=Applied");
        Assert.NotNull(applied);
        Assert.Equal(first.Id, applied.Items.Single().Id);

        var stillApproved = await _client.GetFromJsonAsync<EcoDetailDto>($"/api/ecos/{second.Id}");
        Assert.NotNull(stillApproved);
        Assert.Equal("Approved", stillApproved.Status);

        var revisions = await _client.GetFromJsonAsync<PagedResponse<BomRevisionDto>>(
            $"/api/products/{DemoSeed.Server100Id}/bom-revisions");

        Assert.NotNull(revisions);
        Assert.Single(revisions.Items, revision => revision.Status == "Released");
        Assert.Contains(revisions.Items, revision => revision.RevisionCode == "B" && revision.Status == "Superseded");
        Assert.Contains(revisions.Items, revision => revision.RevisionCode == "C" && revision.Status == "Released");
        Assert.Contains(revisions.Items, revision => revision.RevisionCode == "D" && revision.Status == "Approved");
    }

    /// <summary>Validation codes the ECO form keys off, exercised through real binding.</summary>
    [Fact]
    public async Task CreateValidationReturnsTheDocumentedCodes()
    {
        var duplicateTargets = await PostJsonAsync("/api/ecos", CreateRequest(lines:
        [
            new EcoLineRequest(EngineeringChangeType.ReplaceComponent, DemoSeed.Mb020Id, DemoSeed.Mb021Id, null, null),
            new EcoLineRequest(EngineeringChangeType.ChangeQuantity, DemoSeed.Mb020Id, null, 2, null),
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, duplicateTargets.StatusCode);
        Assert.Equal("duplicate-target-component", Extension(await ProblemAsync(duplicateTargets), "code"));

        var noLines = await PostJsonAsync("/api/ecos", CreateRequest(lines: []));
        Assert.Equal(HttpStatusCode.BadRequest, noLines.StatusCode);
        Assert.Equal("eco-lines-required", Extension(await ProblemAsync(noLines), "code"));

        var blankActor = await PostJsonAsync("/api/ecos", CreateRequest(actor: "   "));
        Assert.Equal(HttpStatusCode.BadRequest, blankActor.StatusCode);
        Assert.Equal("blank-actor", Extension(await ProblemAsync(blankActor), "code"));

        var blankTitle = await PostJsonAsync("/api/ecos", CreateRequest(title: "  "));
        Assert.Equal(HttpStatusCode.BadRequest, blankTitle.StatusCode);
        Assert.Equal("blank-title", Extension(await ProblemAsync(blankTitle), "code"));
        Assert.Equal("application/problem+json", blankTitle.Content.Headers.ContentType?.MediaType);

        var blankReason = await PostJsonAsync(
            "/api/ecos",
            CreateRequest() with { ReasonForChange = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, blankReason.StatusCode);
        Assert.Equal("reason-for-change-required", Extension(await ProblemAsync(blankReason), "code"));
    }

    /// <summary>An unknown ECO is 404 from every ECO endpoint, not a 500 or a validation error.</summary>
    [Fact]
    public async Task AnUnknownEcoIsA404FromEveryEndpoint()
    {
        var missing = Guid.NewGuid();

        var get = await _client.GetAsync($"/api/ecos/{missing}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var put = await _client.PutAsJsonAsync(
            $"/api/ecos/{missing}",
            new UpdateEcoRequest("title", "reason", null, null, [ReplaceMotherboardLine()]),
            ApiJson);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        foreach (var (path, body) in new (string Path, object Body)[]
                 {
                     ("submit", new ActorRequest("Alex Morgan")),
                     ("approve", new ApproveEcoRequest("Jordan Lee", null)),
                     ("reject", new RejectEcoRequest("Jordan Lee", "no")),
                     ("release", new ReleaseEcoRequest("Jordan Lee", DateTime.UtcNow)),
                 })
        {
            var response = await PostJsonAsync($"/api/ecos/{missing}/{path}", body);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    /// <summary>Audit filtering and the invalid-filter contract.</summary>
    [Fact]
    public async Task AuditIsPagedNewestFirstAndRejectsAnUnknownEntityType()
    {
        var created = await CreateEcoAsync();

        var page = await _client.GetFromJsonAsync<PagedResponse<AuditEventDto>>(
            $"/api/audit?entityType=engineeringchangeorder&entityId={created.Id}");

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal("EcoCreated", page.Items[0].Action);

        var invalid = await _client.GetAsync("/api/audit?entityType=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("invalid-filter-value", Extension(await ProblemAsync(invalid), "code"));
    }
}
