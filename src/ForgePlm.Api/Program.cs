using System.Text.Json.Serialization;
using ForgePlm.Api.Application;
using ForgePlm.Api.Endpoints;
using ForgePlm.Api.Http;
using ForgePlm.Api.Persistence;
using ForgePlm.Api.Seed;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ForgePlmDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ForgePlm")));

// Spec §7 / contract: enums are strings on the wire, in both directions. The read DTOs already
// project enums as strings; this makes request bodies bind the same vocabulary (`ReplaceComponent`,
// not `0`) and rejects unknown or numeric values instead of silently coercing them.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

// One service per use case (spec §8). Scoped because each coordinates one DbContext.
builder.Services.AddScoped<CreateEngineeringChange>();
builder.Services.AddScoped<UpdateEngineeringChange>();
builder.Services.AddScoped<SubmitEngineeringChange>();
builder.Services.AddScoped<ApproveEngineeringChange>();
builder.Services.AddScoped<RejectEngineeringChange>();
builder.Services.AddScoped<ReleaseEngineeringChange>();

// A-10/A-21: one mapping from failure to RFC 7807, registered as the framework's exception
// handler so no endpoint can forget it. Endpoints simply let domain exceptions propagate.
builder.Services.AddExceptionHandler<ForgePlmExceptionHandler>();
builder.Services.AddProblemDetails();

// Development-only OpenAPI (spec §4).
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

// A-16: in the demo/run container the API also serves the built SPA, so there is one origin
// and no CORS. Registered only when a build actually produced an index.html — before Slice 7
// there is no frontend, and MapFallbackToFile throws at startup if its target is missing.
app.UseDefaultFiles();
app.UseStaticFiles();

// Explicit rather than relying on the implicit registration from `AddExceptionHandler`:
// `WebApplication.CreateBuilder` adds the developer exception page in Development, which is
// outer in the pipeline and swallows every exception before `IExceptionHandler` sees it. That
// would make Development return a stack trace where the demo returns ProblemDetails — two
// different contracts for the same endpoint, decided by an environment flag nobody would think
// to check. Declaring it here pins the behaviour in both modes.
app.UseExceptionHandler();
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html")))
{
    app.MapFallbackToFile("index.html");
}

// A-11: migrate and seed on startup. `Migrate()` is a no-op once the schema exists, and the
// seed is idempotent by primary key, so repeated starts change nothing.
//
// This is gated on configuration rather than on IsDevelopment() because the container runs as
// Production and still needs the schema. A real deployment would set
// `Database__ApplyOnStartup=false` and run migrations as a separate release step; for a demo
// prototype, one command that leaves a working database behind is the better trade.
if (builder.Configuration.GetValue("Database:ApplyOnStartup", true))
{
    await ApplyDatabaseAsync(app);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Slice 3 — product and BOM reads. Grouped so the surface stays greppable as slices 4-6 add
// the write endpoints beside them.
app.MapProductEndpoints();
app.MapComponentEndpoints();
app.MapBomRevisionEndpoints();

// Slices 4–6 — engineering changes, and the audit read their use cases write.
app.MapEcoEndpoints();
app.MapAuditEndpoints();

app.Run();

static async Task ApplyDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ForgePlmDbContext>();

    await db.Database.MigrateAsync();
    await DemoSeed.ApplyAsync(db);
}

/// <summary>
/// Exposure point for `WebApplicationFactory`. Top-level statements generate an `internal`
/// Program, which the test project cannot name; this partial declaration makes it public so
/// endpoint tests can drive the real pipeline rather than a hand-rolled stand-in.
/// </summary>
public partial class Program;
