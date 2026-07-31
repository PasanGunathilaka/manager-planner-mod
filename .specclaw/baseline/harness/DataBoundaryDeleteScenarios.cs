using ExecutivePlanning.Core.Data;
using ExecutivePlanning.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Baseline.Harness;

/// <summary>
/// Data/persistence boundary: direct PlanningDbContext manipulation, exercising
/// cascade/SetNull/Restrict delete behaviours configured in PlanningDbContext.OnModelCreating
/// that have no PlanningService method to drive them (Objective, Meeting, and User deletion) —
/// the exact pattern the legacy test suite already uses for these cases (e.g.
/// Deleting_project_cascades_to_tasks_and_notes calls t.Db.Projects.Remove(project) directly,
/// read verbatim from PlanningServiceTests.cs).
/// </summary>
public class DataBoundaryDeleteScenarios
{
    [Fact]
    public async Task GM026_DeleteObjective_SetsWorkItemObjectiveIdToNull()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var obj = await svc.AddObjectiveAsync(projectId, "Obj");
        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null, objectiveId: obj.Id);

        // No PlanningService method exists for this (confirmed by reading the whole class) —
        // this is the only way to exercise the rule at all.
        var objective = await t.Db.Objectives.FindAsync(obj.Id);
        t.Db.Objectives.Remove(objective!);
        await t.Db.SaveChangesAsync();

        var reloaded = await t.Db.WorkItems.AsNoTracking().FirstAsync(w => w.Id == task.Id);

        FixtureWriter.Write("GM-026", Paths.FixturesDir,
            input: new { objectiveId = obj.Id, taskId = task.Id },
            output: new { taskSurvives = true, objectiveIdIsNull = reloaded.ObjectiveId is null });

        Assert.Null(reloaded.ObjectiveId);
    }

    [Fact]
    public async Task GM027_DeleteUnassignedElsewhereUser_SetsWorkItemAssigneeIdToNull()
    {
        using var t = new TestDb();
        var (svc, _, _, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        // A team member who owns no project, has authored no note/status-change — so only the
        // Assignee SetNull rule is in play. Deliberately a fresh user, not the ArrangeAsync
        // helper's own member, so no other Restrict rule can interfere.
        var assignee = await svc.AddUserAsync("Assignee Only", "assignee@test", UserRole.TeamMember);
        var task = await svc.AddTaskAsync(projectId, "T", null, assignee.Id, null);

        t.Db.Users.Remove(await t.Db.Users.FindAsync(assignee.Id) ?? throw new InvalidOperationException());
        await t.Db.SaveChangesAsync();

        var reloaded = await t.Db.WorkItems.AsNoTracking().FirstAsync(w => w.Id == task.Id);

        FixtureWriter.Write("GM-027", Paths.FixturesDir,
            input: new { assigneeUserId = assignee.Id, taskId = task.Id },
            output: new { taskSurvives = true, assigneeIdIsNull = reloaded.AssigneeId is null });

        Assert.Null(reloaded.AssigneeId);
    }

    [Fact]
    public async Task GM028_DeleteMeeting_SetsWorkItemAndProgressNoteMeetingLinksToNull()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var meeting = await svc.AddMeetingAsync(projectId, "sync", MeetingType.VideoCall, FixtureWriter.AnchorDate, memberId);
        var task = await svc.AddTaskAsync(projectId, "surprise work", null, memberId, null,
            isDiscovered: true, discoveredInMeetingId: meeting.Id);
        var note = await svc.AddNoteAsync(task.Id, "captured during the meeting", managerId, meetingId: meeting.Id);

        // No PlanningService.DeleteMeetingAsync exists (confirmed by reading the whole class).
        t.Db.Meetings.Remove(await t.Db.Meetings.FindAsync(meeting.Id) ?? throw new InvalidOperationException());
        await t.Db.SaveChangesAsync();

        var reloadedTask = await t.Db.WorkItems.AsNoTracking().FirstAsync(w => w.Id == task.Id);
        var reloadedNote = await t.Db.ProgressNotes.AsNoTracking().FirstAsync(n => n.Id == note.Id);

        FixtureWriter.Write("GM-028", Paths.FixturesDir,
            input: new { meetingId = meeting.Id, taskId = task.Id, noteId = note.Id },
            output: new
            {
                taskSurvives = true,
                noteSurvives = true,
                discoveredInMeetingIdIsNull = reloadedTask.DiscoveredInMeetingId is null,
                noteMeetingIdIsNull = reloadedNote.MeetingId is null
            });

        Assert.Null(reloadedTask.DiscoveredInMeetingId);
        Assert.Null(reloadedNote.MeetingId);
    }

    [Fact]
    public async Task GM029_DeleteChecklistItemAssignee_SetsChecklistItemAssigneeIdToNull()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null);
        var assignee = await svc.AddUserAsync("Checklist Assignee", "checklist-assignee@test", UserRole.TeamMember);
        var item = await svc.AddChecklistItemAsync(task.Id, "step", assigneeId: assignee.Id);

        t.Db.Users.Remove(await t.Db.Users.FindAsync(assignee.Id) ?? throw new InvalidOperationException());
        await t.Db.SaveChangesAsync();

        var reloaded = await t.Db.ChecklistItems.AsNoTracking().FirstAsync(c => c.Id == item.Id);

        FixtureWriter.Write("GM-029", Paths.FixturesDir,
            input: new { checklistAssigneeUserId = assignee.Id, checklistItemId = item.Id },
            output: new { checklistItemSurvives = true, assigneeIdIsNull = reloaded.AssigneeId is null });

        Assert.Null(reloaded.AssigneeId);
    }

    [Fact]
    public async Task GM030_DeleteUserWhoOwnsProject_Throws_Restrict()
    {
        using var t = new TestDb();
        var (_, managerId, _, projectId) = await Arrange.ManagerMemberProjectAsync(t);

        var result = await Capture.RunAsync(async () =>
        {
            t.Db.Users.Remove(await t.Db.Users.FindAsync(managerId) ?? throw new InvalidOperationException());
            await t.Db.SaveChangesAsync();
        });

        var userSurvives = await t.Db.Users.AnyAsync(u => u.Id == managerId);
        var projectSurvives = await t.Db.Projects.AnyAsync(p => p.Id == projectId);

        FixtureWriter.Write("GM-030", Paths.FixturesDir,
            input: new { managerUserId = managerId, ownedProjectId = projectId },
            output: new { result, userSurvives, projectSurvives });

        Assert.True(result.Threw); // an EF Core DbUpdateException wrapping the SQLite FK-constraint violation
        Assert.True(userSurvives);
        Assert.True(projectSurvives);
    }

    [Fact]
    public async Task GM031_DeleteUserWhoAuthoredStatusChange_Throws_Restrict()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        // A second manager who owns no project, so only the StatusChange.ChangedBy Restrict
        // rule is in play here — not the User->Project Restrict rule GM-030 already pins.
        var changer = await svc.AddUserAsync("Status Changer", "changer@test", UserRole.Manager);
        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null);
        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, changer.Id);

        var result = await Capture.RunAsync(async () =>
        {
            t.Db.Users.Remove(await t.Db.Users.FindAsync(changer.Id) ?? throw new InvalidOperationException());
            await t.Db.SaveChangesAsync();
        });

        var userSurvives = await t.Db.Users.AnyAsync(u => u.Id == changer.Id);
        var statusChangeSurvives = await t.Db.StatusChanges.AnyAsync(s => s.ChangedById == changer.Id);

        FixtureWriter.Write("GM-031", Paths.FixturesDir,
            input: new { changerUserId = changer.Id, taskId = task.Id },
            output: new { result, userSurvives, statusChangeSurvives });

        Assert.True(result.Threw);
        Assert.True(userSurvives);
        Assert.True(statusChangeSurvives);
    }

    [Fact]
    public async Task GM032_DeleteChecklistItemWithChildren_RestrictOnlyBitesWhenChildNotAlreadyTracked()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null);
        var parent = await svc.AddChecklistItemAsync(task.Id, "parent");
        var child = await svc.AddChecklistItemAsync(task.Id, "child", parentId: parent.Id);

        // Sub-case A: a FRESH DbContext sharing the same underlying SQLite connection, which
        // has never loaded `child` and so cannot perform any client-side navigation fixup. This
        // is the genuine, raw schema-level test of the self-referencing Restrict rule that
        // scenarios.md's own caveat describes ("this specific raw operation throws") — the DELETE
        // reaches SQLite as-is and the FK constraint (enforced via the Sqlite provider's
        // automatic PRAGMA foreign_keys=ON) rejects it.
        var freshContextResult = await Capture.RunAsync(async () =>
        {
            using var freshDb = NewContextSharingConnection(t);
            var parentOnly = await freshDb.ChecklistItems.FindAsync(parent.Id);
            freshDb.ChecklistItems.Remove(parentOnly!);
            await freshDb.SaveChangesAsync();
        });

        var afterFreshContextAttempt = new
        {
            parentSurvives = await t.Db.ChecklistItems.AsNoTracking().AnyAsync(c => c.Id == parent.Id),
            childSurvives = await t.Db.ChecklistItems.AsNoTracking().AnyAsync(c => c.Id == child.Id)
        };

        // Sub-case B — an empirically-discovered nuance found by actually running this harness,
        // not assumed by scenarios.md's design-time prediction: when the SAME DbContext instance
        // that originally added both `parent` and `child` (both still tracked — exactly the
        // pattern every other scenario in this harness, and the legacy app's own real desktop
        // sessions, use) performs the delete, EF Core's own client-side relationship fixup
        // silently sets child.ParentId to null and the delete succeeds with NO exception at all.
        // Confirmed directly, not guessed: the schema-configured Restrict rule only actually
        // protects the data when the dependent isn't already tracked in the acting context —
        // this is exactly a TARGET-GAP question worth raising in /specclaw:clarify (should the
        // rebuild's equivalent guard against this regardless of tracking state?).
        var sameContextResult = await Capture.RunAsync(async () =>
        {
            var parentEntity = await t.Db.ChecklistItems.FindAsync(parent.Id);
            t.Db.ChecklistItems.Remove(parentEntity!);
            await t.Db.SaveChangesAsync();
        });

        var childAfterSameContextAttempt = await t.Db.ChecklistItems.AsNoTracking().FirstOrDefaultAsync(c => c.Id == child.Id);
        var parentSurvivesAfterSameContextAttempt = await t.Db.ChecklistItems.AsNoTracking().AnyAsync(c => c.Id == parent.Id);

        FixtureWriter.Write("GM-032", Paths.FixturesDir,
            input: new { parentChecklistItemId = parent.Id, childChecklistItemId = child.Id },
            output: new
            {
                freshContextAttempt = new
                {
                    result = freshContextResult,
                    afterFreshContextAttempt.parentSurvives,
                    afterFreshContextAttempt.childSurvives
                },
                sameContextAttempt = new
                {
                    result = sameContextResult,
                    parentSurvives = parentSurvivesAfterSameContextAttempt,
                    childSurvives = childAfterSameContextAttempt != null,
                    childParentIdAfter = childAfterSameContextAttempt?.ParentId
                }
            });

        // Raw schema-level Restrict rule DOES throw when the child isn't already tracked in the
        // acting context...
        Assert.True(freshContextResult.Threw);
        Assert.True(afterFreshContextAttempt.parentSurvives);
        Assert.True(afterFreshContextAttempt.childSurvives);
        // ...but does NOT throw when both rows are already tracked in the same context
        // performing the delete — EF Core's own client-side fixup nulls the child's FK first.
        Assert.False(sameContextResult.Threw);
        Assert.NotNull(childAfterSameContextAttempt);
        Assert.Null(childAfterSameContextAttempt!.ParentId);
    }

    private static PlanningDbContext NewContextSharingConnection(TestDb t)
    {
        // Passing the already-open DbConnection instance directly (not a new connection string)
        // means EF Core does not take ownership of its lifecycle, so disposing this context does
        // not close the shared in-memory SQLite connection `t.Db` continues to use afterward —
        // confirmed directly by running this harness (t.Db remains fully queryable after this
        // context is disposed), not assumed.
        var options = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseSqlite(t.Db.Database.GetDbConnection())
            .Options;
        return new PlanningDbContext(options);
    }

    [Fact]
    public async Task GM033_DeleteUser_OnlyOwnsTaskViaTaskOwner_CascadesOwnershipRow()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var owner = await svc.AddUserAsync("Owner Only", "owner-only@test", UserRole.TeamMember);
        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null); // memberId is Assignee, not TaskOwner
        await svc.SetOwnersAsync(task.Id, new[] { owner.Id });

        t.Db.Users.Remove(await t.Db.Users.FindAsync(owner.Id) ?? throw new InvalidOperationException());
        await t.Db.SaveChangesAsync();

        var taskSurvives = await t.Db.WorkItems.AnyAsync(w => w.Id == task.Id);
        var ownershipRowSurvives = await t.Db.TaskOwners.AnyAsync(o => o.UserId == owner.Id);

        FixtureWriter.Write("GM-033", Paths.FixturesDir,
            input: new { ownerOnlyUserId = owner.Id, taskId = task.Id },
            output: new { taskSurvives, ownershipRowSurvives });

        Assert.True(taskSurvives);
        Assert.False(ownershipRowSurvives);
    }
}
