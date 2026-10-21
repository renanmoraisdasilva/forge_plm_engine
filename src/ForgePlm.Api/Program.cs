using ForgePlm.Api.Persistence;
using ForgePlm.Api.Seed;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ForgePlmDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ForgePlm")));

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

app.Run();

static async Task ApplyDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ForgePlmDbContext>();

    await db.Database.MigrateAsync();
    await DemoSeed.ApplyAsync(db);
}
