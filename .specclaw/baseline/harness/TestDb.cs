using ExecutivePlanning.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Baseline.Harness;

/// <summary>
/// Builds a real (in-memory) SQLite database, imitating — not reinventing — the exact arrange
/// pattern already proven in the legacy repo's own test suite at
/// ../../../../manager-planner/tests/ExecutivePlanning.Tests/TestDb.cs (read directly): a real
/// SqliteConnection("DataSource=:memory:") kept open for the test's lifetime, not a fake
/// in-memory provider.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public PlanningDbContext Db { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // keep open so the in-memory DB survives for the test's lifetime

        var options = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new PlanningDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
