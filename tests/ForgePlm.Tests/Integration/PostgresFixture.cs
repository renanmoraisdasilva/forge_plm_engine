using ForgePlm.Api.Persistence;
using ForgePlm.Api.Seed;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ForgePlm.Tests.Integration;

/// <summary>
/// Creates a dedicated PostgreSQL database per test (spec §11), applies migrations, and drops it
/// afterwards. Runs against the same container as the demo (spec §4), so these are honest
/// relational tests — not the in-memory provider, which §11 forbids for any claim about
/// transactions, uniqueness or concurrency.
///
/// One database per test rather than one per run: the first version shared a single database
/// across the whole collection, and a test that inserted its own `SERVER-100` made every other
/// test's seed fail with a part-number unique violation. Isolation is what lets tests be
/// independent of execution order.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Host/credentials for the Compose PostgreSQL container.</summary>
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=forge;Password=forge_dev_password;Include Error Detail=true";

    /// <summary>
    /// Unique to this fixture instance, so two `dotnet test` processes sharing the container
    /// cannot drop each other's databases. The old sweep matched every `forge_plm_test_%`
    /// database, which was correct only while one process existed.
    /// </summary>
    private readonly string _runPrefix = $"forge_plm_test_{Guid.NewGuid():N}_";

    private int _sequence;

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Sweeps any database left by *this run* — a crashed test, or a process killed before
    /// disposal — without touching another run's databases.
    /// </summary>
    public async Task DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();

        var leftovers = new List<string>();
        await using (var query = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE starts_with(datname, @prefix)", admin))
        {
            query.Parameters.AddWithValue("prefix", _runPrefix);

            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                leftovers.Add(reader.GetString(0));
            }
        }

        foreach (var name in leftovers)
        {
            await DropAsync(admin, name);
        }
    }

    /// <summary>Creates, migrates, and hands back a database that belongs to exactly one test.</summary>
    public async Task<IsolatedDatabase> CreateDatabaseAsync()
    {
        var name = $"{_runPrefix}{Interlocked.Increment(ref _sequence):D4}";

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();

            // Database names come from a Guid, but quoting is still correct practice.
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var database = new IsolatedDatabase(name);

        try
        {
            await using var db = database.CreateDbContext();
            await db.Database.MigrateAsync();
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }

        return database;
    }

    private static async Task DropAsync(NpgsqlConnection admin, string name)
    {
        // Terminate other connections first: PostgreSQL refuses to drop a database with live
        // connections, and a pooled connector can outlive the DbContext that opened it.
        await using (var terminate = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{name}' AND pid <> pg_backend_pid()",
            admin))
        {
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\"", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// A test-owned database, disposed at the end of the test that created it.
/// </summary>
public sealed class IsolatedDatabase : IAsyncDisposable
{
    private readonly string _name;

    public IsolatedDatabase(string name) => _name = name;

    /// <summary>
    /// `Pooling=false` keeps a closed DbContext's connector out of the pool, so disposal really
    /// does release the database and DROP cannot race an idle pooled connection.
    /// </summary>
    public string ConnectionString =>
        $"Host=localhost;Port=5432;Database={_name};Username=forge;Password=forge_dev_password;Include Error Detail=true;Pooling=false";

    public ForgePlmDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ForgePlmDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ForgePlmDbContext(options);
    }

    /// <summary>Applies the seed, as startup would (A-11).</summary>
    public async Task SeedAsync()
    {
        await using var db = CreateDbContext();
        await DemoSeed.ApplyAsync(db);
    }

    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(
            "Host=localhost;Port=5432;Database=postgres;Username=forge;Password=forge_dev_password;Include Error Detail=true");
        await admin.OpenAsync();

        await using (var terminate = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_name}' AND pid <> pg_backend_pid()",
            admin))
        {
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\"", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
