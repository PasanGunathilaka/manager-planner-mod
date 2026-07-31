using ExecutivePlanning.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Baseline.Harness;

/// <summary>
/// Stateful service boundary: PlanningService.ChangeStatusAsync (:184-205), SetOwnersAsync
/// (:172-179), ToggleChecklistItemAsync (:162-169). CompletedUtc values are recorded but only
/// their non-null-ness is asserted (seams.md CB-1's Option 2) — no documented business rule
/// reads the exact CompletedUtc/ChecklistItem.CompletedUtc timestamp back for a decision.
/// </summary>
public class StatusAndOwnershipScenarios
{
    [Fact]
    public async Task GM019_ChangeStatus_SameStatusTransition_IsNoop()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "Do work", null, memberId, null); // default status: NotStarted

        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.NotStarted, managerId);

        var reloaded = await svc.GetTaskAsync(task.Id);

        FixtureWriter.Write("GM-019", Paths.FixturesDir,
            input: new { fromStatus = "NotStarted", toStatus = "NotStarted" },
            output: new { statusHistoryCount = reloaded!.StatusHistory.Count });

        Assert.Empty(reloaded!.StatusHistory);
    }

    [Fact]
    public async Task GM020_ChangeStatus_IntoDone_SetsCompletedUtc()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "Do work", null, memberId, null);
        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, managerId);

        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done, managerId);

        var reloaded = await svc.GetTaskAsync(task.Id);

        FixtureWriter.Write("GM-020", Paths.FixturesDir,
            input: new { fromStatus = "InProgress", toStatus = "Done" },
            output: new
            {
                status = reloaded!.Status.ToString(),
                completedUtcIsNull = reloaded.CompletedUtc is null,
                completedUtc = reloaded.CompletedUtc?.ToString("o")
            },
            normalizedFields: new[] { "output.completedUtc" });

        Assert.Equal(WorkItemStatus.Done, reloaded!.Status);
        Assert.NotNull(reloaded.CompletedUtc);
    }

    [Fact]
    public async Task GM021_ChangeStatus_OutOfDone_ClearsCompletedUtcBackToNull()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "Do work", null, memberId, null);
        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done, managerId);

        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, managerId);

        var reloaded = await svc.GetTaskAsync(task.Id);

        FixtureWriter.Write("GM-021", Paths.FixturesDir,
            input: new { fromStatus = "Done", toStatus = "InProgress" },
            output: new { status = reloaded!.Status.ToString(), completedUtcIsNull = reloaded.CompletedUtc is null });

        Assert.Equal(WorkItemStatus.InProgress, reloaded!.Status);
        Assert.Null(reloaded.CompletedUtc); // pinned exactly as legacy behavior, regardless of CQ-018's open "is this a defect" question
    }

    [Fact]
    public async Task GM022_SetOwners_ReplacesFullSet_DoesNotAppend()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "Do work", null, memberId, null);

        await svc.SetOwnersAsync(task.Id, new[] { memberId, managerId });
        await svc.SetOwnersAsync(task.Id, new[] { memberId }); // strict subset, dropping managerId

        var owners = await t.Db.TaskOwners.Where(o => o.WorkItemId == task.Id).Select(o => o.UserId).ToListAsync();

        FixtureWriter.Write("GM-022", Paths.FixturesDir,
            input: new { firstSet = new[] { memberId, managerId }, secondSet = new[] { memberId } },
            output: new { remainingOwnerUserIds = owners });

        Assert.Equal(new[] { memberId }, owners);
    }

    [Fact]
    public async Task GM023_ToggleChecklistItem_StampsAndClearsCompletedUtc_BothWays()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "Do work", null, memberId, null);
        var item = await svc.AddChecklistItemAsync(task.Id, "step 1");

        await svc.ToggleChecklistItemAsync(item.Id, true);
        var afterDone = await t.Db.ChecklistItems.AsNoTracking().FirstAsync(c => c.Id == item.Id);

        await svc.ToggleChecklistItemAsync(item.Id, false);
        var afterUndone = await t.Db.ChecklistItems.AsNoTracking().FirstAsync(c => c.Id == item.Id);

        FixtureWriter.Write("GM-023", Paths.FixturesDir,
            input: new { toggleSequence = new[] { true, false } },
            output: new
            {
                afterDone = new { afterDone.IsDone, completedUtcIsNull = afterDone.CompletedUtc is null },
                afterUndone = new { afterUndone.IsDone, completedUtcIsNull = afterUndone.CompletedUtc is null }
            });

        Assert.True(afterDone.IsDone);
        Assert.NotNull(afterDone.CompletedUtc);
        Assert.False(afterUndone.IsDone);
        Assert.Null(afterUndone.CompletedUtc);
    }
}
