using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ManagerPlanner.Core.Data;

/// <summary>
/// Lets EF Core design-time tooling (<c>dotnet ef migrations add</c>, <c>dotnet ef database
/// update</c>) construct a <see cref="PlanningDbContext"/> without a running host — the actual
/// runtime connection string lives in ManagerPlanner.Web's appsettings.json.
/// </summary>
public class PlanningDbContextFactory : IDesignTimeDbContextFactory<PlanningDbContext>
{
    public PlanningDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseSqlite("Data Source=designtime.db")
            .Options;

        return new PlanningDbContext(options);
    }
}
