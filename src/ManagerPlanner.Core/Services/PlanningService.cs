using ManagerPlanner.Core.Data;
using ManagerPlanner.Core.Domain;
using ManagerPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;

namespace ManagerPlanner.Core.Services;

/// <summary>
/// Application service over the planning database. Each method opens and disposes its own
/// short-lived <see cref="PlanningDbContext"/> via the factory rather than holding one for the
/// service's own lifetime, to avoid sharing a context across a Blazor Server circuit.
/// </summary>
public class PlanningService
{
    private readonly IDbContextFactory<PlanningDbContext> _dbFactory;

    public PlanningService(IDbContextFactory<PlanningDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<List<Project>> GetProjectsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Projects
            .Include(p => p.Owner)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync();
    }

    public async Task<Project> AddProjectAsync(string name, string? description, int ownerId)
    {
        PlanningRules.ValidateProjectName(name);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = new Project { Name = name.Trim(), Description = description?.Trim(), OwnerId = ownerId };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    public async Task<ProjectSummary> GetProjectSummaryAsync(int projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;
        var tasks = await db.WorkItems.Where(t => t.ProjectId == projectId).ToListAsync();
        var project = await db.Projects.FindAsync(projectId);

        return new ProjectSummary
        {
            ProjectId = projectId,
            ProjectName = project?.Name ?? string.Empty,
            TotalTasks = tasks.Count,
            Done = tasks.Count(t => t.Status == WorkItemStatus.Done),
            InProgress = tasks.Count(t => t.Status == WorkItemStatus.InProgress),
            Blocked = tasks.Count(t => t.Status == WorkItemStatus.Blocked),
            NotStarted = tasks.Count(t => t.Status == WorkItemStatus.NotStarted),
            Overdue = tasks.Count(t => t.Deadline.HasValue && t.Deadline.Value < now && t.Status != WorkItemStatus.Done),
            Discovered = tasks.Count(t => t.IsDiscovered)
        };
    }

    /// <summary>
    /// Returns the Id of the single bootstrapped Manager user. No legacy equivalent — this app has
    /// neither authentication nor DbSeeder-style sample data yet, so there is no other source for
    /// the "current user" the legacy desktop apps resolved once at ViewModel startup.
    /// </summary>
    public async Task<int> GetCurrentManagerIdAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users
            .Where(u => u.Role == UserRole.Manager)
            .Select(u => u.Id)
            .FirstAsync();
    }

    public async Task<Objective> AddObjectiveAsync(int projectId, string title, string? keyResult = null)
    {
        PlanningRules.ValidateObjectiveTitle(title);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var order = await db.Objectives.Where(o => o.ProjectId == projectId).CountAsync();
        var objective = new Objective
        {
            ProjectId = projectId,
            Title = title.Trim(),
            KeyResult = keyResult,
            SortOrder = order
        };
        db.Objectives.Add(objective);
        await db.SaveChangesAsync();
        return objective;
    }

    /// <summary>
    /// Loads the full planner grid for a project: objectives → tasks → owners + nested checklist.
    /// Tasks will be empty until backlog item 3 (Task/WorkItem) exists — expected, not a bug.
    /// </summary>
    public async Task<List<Objective>> GetPlannerForProjectAsync(int projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Objectives
            .Where(o => o.ProjectId == projectId)
            .OrderBy(o => o.SortOrder)
            .Include(o => o.Tasks).ThenInclude(t => t.Assignee)
            .Include(o => o.Tasks).ThenInclude(t => t.Owners).ThenInclude(w => w.User)
            .Include(o => o.Tasks).ThenInclude(t => t.Checklist)
            .ToListAsync();
    }
}
