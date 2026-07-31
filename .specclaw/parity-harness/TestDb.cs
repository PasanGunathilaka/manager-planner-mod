using ManagerPlanner.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ParityHarness;

/// <summary>
/// Real (in-memory) SQLite database wrapped in an IDbContextFactory, since the modern
/// PlanningService takes IDbContextFactory&lt;PlanningDbContext&gt; and opens/disposes its own
/// short-lived DbContext per call rather than holding one for the service's lifetime (confirmed by
/// reading src/ManagerPlanner.Core/Services/PlanningService.cs directly). Mirrors the sibling
/// precedent at .specclaw/baseline/harness/TestDb.cs (real SqliteConnection("DataSource=:memory:")
/// kept open for the test's lifetime), adapted to the factory shape.
/// </summary>
public sealed class TestDb : IDbContextFactory<PlanningDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PlanningDbContext> _options;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new PlanningDbContext(_options);
        db.Database.EnsureCreated();
    }

    public PlanningDbContext CreateDbContext() => new PlanningDbContext(_options);

    public Task<PlanningDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    /// <summary>Convenience for arrange steps: a fresh context the caller disposes itself.</summary>
    public PlanningDbContext NewContext() => CreateDbContext();

    public void Dispose() => _connection.Dispose();
}
