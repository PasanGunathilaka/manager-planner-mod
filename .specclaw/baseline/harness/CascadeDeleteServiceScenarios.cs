using ExecutivePlanning.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Baseline.Harness;

/// <summary>
/// Stateful service boundary: PlanningService.DeleteProjectAsync (:64-70) and DeleteTaskAsync
/// (:73-79), backed by the Cascade delete behaviours configured in
/// PlanningDbContext.OnModelCreating.
/// </summary>
public class CascadeDeleteServiceScenarios
{
    [Fact]
    public async Task GM024_DeleteProject_CascadesToEverythingUnderIt()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var obj = await svc.AddObjectiveAsync(projectId, "Obj");
        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null, objectiveId: obj.Id);
        var parent = await svc.AddChecklistItemAsync(task.Id, "parent");
        await svc.AddChecklistItemAsync(task.Id, "child", parentId: parent.Id);
        await svc.AddNoteAsync(task.Id, "note", managerId);
        await svc.SetOwnersAsync(task.Id, new[] { memberId });
        await svc.AddMeetingAsync(projectId, "sync", MeetingType.VideoCall, FixtureWriter.AnchorDate, memberId);

        await svc.DeleteProjectAsync(projectId);

        var counts = new
        {
            projects = await t.Db.Projects.CountAsync(),
            objectives = await t.Db.Objectives.CountAsync(),
            workItems = await t.Db.WorkItems.CountAsync(),
            checklistItems = await t.Db.ChecklistItems.CountAsync(),
            progressNotes = await t.Db.ProgressNotes.CountAsync(),
            taskOwners = await t.Db.TaskOwners.CountAsync(),
            meetings = await t.Db.Meetings.CountAsync()
        };

        FixtureWriter.Write("GM-024", Paths.FixturesDir,
            input: new { projectId, objectiveId = obj.Id, taskId = task.Id, parentChecklistId = parent.Id },
            output: counts);

        Assert.Equal(0, counts.projects);
        Assert.Equal(0, counts.objectives);
        Assert.Equal(0, counts.workItems);
        Assert.Equal(0, counts.checklistItems);
        Assert.Equal(0, counts.progressNotes);
        Assert.Equal(0, counts.taskOwners);
        Assert.Equal(0, counts.meetings);
    }

    [Fact]
    public async Task GM025_DeleteTask_CascadesToChecklistNotesOwnersStatusHistory()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null);
        var parent = await svc.AddChecklistItemAsync(task.Id, "parent");
        await svc.AddChecklistItemAsync(task.Id, "child", parentId: parent.Id);
        await svc.AddNoteAsync(task.Id, "note", managerId);
        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, managerId);
        await svc.SetOwnersAsync(task.Id, new[] { memberId });

        await svc.DeleteTaskAsync(task.Id);

        var counts = new
        {
            workItems = await t.Db.WorkItems.CountAsync(),
            checklistItems = await t.Db.ChecklistItems.CountAsync(),
            progressNotes = await t.Db.ProgressNotes.CountAsync(),
            taskOwners = await t.Db.TaskOwners.CountAsync(),
            statusChanges = await t.Db.StatusChanges.CountAsync()
        };

        FixtureWriter.Write("GM-025", Paths.FixturesDir,
            input: new { projectId, taskId = task.Id, parentChecklistId = parent.Id },
            output: counts);

        Assert.Equal(0, counts.workItems);
        Assert.Equal(0, counts.checklistItems);
        Assert.Equal(0, counts.progressNotes);
        Assert.Equal(0, counts.taskOwners);
        Assert.Equal(0, counts.statusChanges);
    }
}
