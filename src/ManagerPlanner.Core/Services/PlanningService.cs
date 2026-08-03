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
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<WorkItem> AddTaskAsync(int projectId, string title, string? description,
        int? assigneeId, DateTime? deadline, bool isDiscovered = false, int? objectiveId = null)
    {
        PlanningRules.ValidateTaskTitle(title);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var task = new WorkItem
        {
            ProjectId = projectId,
            ObjectiveId = objectiveId,
            Title = title.Trim(),
            Description = description,
            AssigneeId = assigneeId,
            Deadline = deadline,
            IsDiscovered = isDiscovered
        };
        db.WorkItems.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    public async Task<List<User>> GetTeamMembersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Users
            .Where(u => u.Role == UserRole.TeamMember && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    /// <summary>
    /// Tasks with no objective — new, no legacy equivalent. Manager Planner Desktop's legacy grid
    /// never rendered this case (its only add-task path always supplied an objective).
    /// </summary>
    public async Task<List<WorkItem>> GetUngroupedTasksForProjectAsync(int projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WorkItems
            .Where(t => t.ProjectId == projectId && t.ObjectiveId == null)
            .Include(t => t.Assignee)
            .Include(t => t.Owners).ThenInclude(o => o.User)
            .Include(t => t.Checklist)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task ChangeStatusAsync(int taskId, WorkItemStatus newStatus, int changedById, string? reason = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var task = await db.WorkItems.FirstOrDefaultAsync(t => t.Id == taskId)
                   ?? throw new InvalidOperationException($"Task {taskId} not found.");

        if (task.Status == newStatus) return;

        var change = new StatusChange
        {
            WorkItemId = task.Id,
            FromStatus = task.Status,
            ToStatus = newStatus,
            ChangedById = changedById,
            Reason = reason
        };

        task.Status = newStatus;
        task.CompletedUtc = newStatus == WorkItemStatus.Done ? DateTime.UtcNow : null;

        db.StatusChanges.Add(change);
        await db.SaveChangesAsync();
    }

    public async Task ToggleChecklistItemAsync(int itemId, bool isDone)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var item = await db.ChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId)
                   ?? throw new InvalidOperationException($"Checklist item {itemId} not found.");

        item.IsDone = isDone;
        item.CompletedUtc = isDone ? DateTime.UtcNow : null;

        await db.SaveChangesAsync();
    }

    public async Task<List<Meeting>> GetMeetingsForProjectAsync(int projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Meetings
            .Include(m => m.Participant)
            .Where(m => m.ProjectId == projectId)
            .OrderByDescending(m => m.MeetingDate)
            .ToListAsync();
    }

    public async Task<Meeting> AddMeetingAsync(int projectId, string title, MeetingType type,
        DateTime meetingDate, int? participantId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var meeting = new Meeting
        {
            ProjectId = projectId,
            Title = title,
            Type = type,
            MeetingDate = meetingDate,
            ParticipantId = participantId
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return meeting;
    }

    public async Task<ProgressNote> AddNoteAsync(int taskId, string text, int authorId,
        int? meetingId = null, bool isPromise = false, DateTime? promisedDate = null,
        DateTime? noteDate = null)
    {
        PlanningRules.ValidateNoteText(text);
        var effectiveDate = noteDate ?? DateTime.UtcNow;
        PlanningRules.ValidateNoteDate(effectiveDate);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var note = new ProgressNote
        {
            WorkItemId = taskId,
            Text = text.Trim(),
            AuthorId = authorId,
            MeetingId = meetingId,
            IsPromise = isPromise,
            PromisedDate = promisedDate,
            NoteDate = effectiveDate
        };
        db.ProgressNotes.Add(note);
        await db.SaveChangesAsync();
        return note;
    }

    public async Task<List<ProgressNote>> GetNotesForTaskAsync(int taskId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ProgressNotes
            .Include(n => n.Author)
            .Include(n => n.Meeting)
            .Where(n => n.WorkItemId == taskId)
            .OrderByDescending(n => n.NoteDate)
            .ToListAsync();
    }

    /// <summary>
    /// Builds the promised-vs-delivered accountability report for a project. For each task it takes
    /// the most recent promise note and compares it against the task's actual status/completion.
    /// </summary>
    public async Task<List<AccountabilityRow>> GetAccountabilityReportAsync(int projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;

        var tasks = await db.WorkItems
            .Include(t => t.Assignee)
            .Include(t => t.Project)
            .Include(t => t.Notes)
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        var rows = new List<AccountabilityRow>();
        foreach (var t in tasks)
        {
            var latestPromise = t.Notes
                .Where(n => n.IsPromise && n.PromisedDate.HasValue)
                .OrderByDescending(n => n.CreatedUtc)
                .FirstOrDefault();

            var row = new AccountabilityRow
            {
                WorkItemId = t.Id,
                TaskTitle = t.Title,
                ProjectName = t.Project?.Name ?? string.Empty,
                AssigneeName = t.Assignee?.FullName ?? "(unassigned)",
                Status = t.Status,
                Deadline = t.Deadline,
                CompletedUtc = t.CompletedUtc,
                LatestPromisedDate = latestPromise?.PromisedDate,
                LatestPromiseText = latestPromise?.Text,
                LatestPromiseRecordedUtc = latestPromise?.CreatedUtc
            };

            row.IsOverdue = t.Deadline.HasValue
                            && t.Deadline.Value < now
                            && t.Status != WorkItemStatus.Done;

            if (latestPromise?.PromisedDate is DateTime promised)
            {
                if (t.Status == WorkItemStatus.Done)
                {
                    // Delivered — kept only if completed on or before the promised date.
                    row.PromiseKept = t.CompletedUtc.HasValue && t.CompletedUtc.Value.Date <= promised.Date;
                    row.PromiseBroken = !row.PromiseKept;
                }
                else
                {
                    // Not delivered — broken once the promised date has passed.
                    row.PromiseBroken = promised.Date < now.Date;
                }
            }

            rows.Add(row);
        }

        // Most at-risk first: broken promises, then overdue, then the rest.
        return rows
            .OrderByDescending(r => r.PromiseBroken)
            .ThenByDescending(r => r.IsOverdue)
            .ThenBy(r => r.Deadline ?? DateTime.MaxValue)
            .ToList();
    }

    /// <summary>Accountability report across every project, most-at-risk first.</summary>
    public async Task<List<AccountabilityRow>> GetAccountabilityForAllProjectsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var rows = new List<AccountabilityRow>();
        var projectIds = await db.Projects.OrderBy(p => p.Name).Select(p => p.Id).ToListAsync();
        foreach (var id in projectIds)
            rows.AddRange(await GetAccountabilityReportAsync(id));

        return rows
            .OrderByDescending(r => r.PromiseBroken)
            .ThenByDescending(r => r.IsOverdue)
            .ThenBy(r => r.ProjectName)
            .ThenBy(r => r.Deadline ?? DateTime.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Deletes a task and its checklist, notes, owners and status history (cascade).
    /// Unlike the legacy body (a plain FindAsync + Remove), this loads the Checklist
    /// collection first. ChecklistItem.ParentId is Restrict (self-reference), not Cascade,
    /// so SQLite's own FK-cascade engine can't resolve a multi-level checklist tree from a
    /// cold context — it needs the child rows already tracked so EF Core's client-side
    /// cascade (which orders self-referencing deletes correctly) runs instead of the raw
    /// DB cascade. Confirmed by direct reproduction: this exact call shape throws
    /// "FOREIGN KEY constraint failed" against a fresh IDbContextFactory-created context
    /// whenever the task has a parent+child checklist item — every call in this app gets a
    /// fresh context, so this isn't an edge case, it's every call with a nested checklist.
    /// </summary>
    public async Task DeleteTaskAsync(int taskId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var t = await db.WorkItems.Include(w => w.Checklist).FirstOrDefaultAsync(w => w.Id == taskId);
        if (t is null) return;

        db.WorkItems.Remove(t);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a project and everything under it (objectives, tasks, meetings, and
    /// transitively each task's checklist/notes/status history/owners) — cascade.
    /// Same reasoning as DeleteTaskAsync applies one level deeper: this loads each task's
    /// Checklist collection first, since ChecklistItem.ParentId is Restrict (self-reference)
    /// and SQLite's own FK-cascade engine can't resolve that multi-level tree from a cold,
    /// untracked context. Do not simplify this back to a plain FindAsync + Remove.
    /// </summary>
    public async Task DeleteProjectAsync(int projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var p = await db.Projects.Include(pr => pr.Tasks).ThenInclude(t => t.Checklist)
            .FirstOrDefaultAsync(pr => pr.Id == projectId);
        if (p is null) return;

        db.Projects.Remove(p);
        await db.SaveChangesAsync();
    }
}
