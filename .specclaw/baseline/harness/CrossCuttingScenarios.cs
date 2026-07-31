using ExecutivePlanning.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Baseline.Harness;

/// <summary>
/// GM-039 pins the dual-ownership-mechanism coexistence (WorkItem.AssigneeId vs. the TaskOwner
/// many-to-many) exactly as the legacy app behaves today, without resolving which mechanism is
/// "correct" — that is CQ-005's open DECISION question, not this fixture's job. GM-040 pins
/// AddTaskAsync's discoveredInMeetingId parameter, which is reachable through PlanningService
/// directly but never exercised by either desktop UI.
/// </summary>
public class CrossCuttingScenarios
{
    [Fact]
    public async Task GM039_DualOwnershipMechanisms_CoexistWithoutReconciliation()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var otherMember = await svc.AddUserAsync("Other Member", "other-member@test", UserRole.TeamMember);

        var task = await svc.AddTaskAsync(projectId, "T", null, memberId, null); // AssigneeId = memberId
        await svc.SetOwnersAsync(task.Id, new[] { otherMember.Id }); // a completely different person, deliberately excluding memberId

        // PlanningService.GetTaskAsync does not Include(t => t.Owners) — query the context
        // directly so both mechanisms are visible on the same reload.
        var reloaded = await t.Db.WorkItems
            .Include(w => w.Assignee)
            .Include(w => w.Owners).ThenInclude(o => o.User)
            .AsNoTracking()
            .FirstAsync(w => w.Id == task.Id);

        FixtureWriter.Write("GM-039", Paths.FixturesDir,
            input: new { assigneeUserId = memberId, ownerUserIds = new[] { otherMember.Id } },
            output: new
            {
                assigneeId = reloaded.AssigneeId,
                ownerUserIds = reloaded.Owners.Select(o => o.UserId).ToArray()
            });

        Assert.Equal(memberId, reloaded.AssigneeId);
        Assert.Equal(new[] { otherMember.Id }, reloaded.Owners.Select(o => o.UserId).ToArray());
    }

    [Fact]
    public async Task GM040_AddTask_SetsDiscoveredInMeetingId_ServiceReachable_UIUnreachable()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var meeting = await svc.AddMeetingAsync(projectId, "sync", MeetingType.VideoCall, FixtureWriter.AnchorDate, memberId);

        var task = await svc.AddTaskAsync(projectId, "surprise work", null, memberId,
            FixtureWriter.AnchorDate.AddDays(2), isDiscovered: true, discoveredInMeetingId: meeting.Id);

        FixtureWriter.Write("GM-040", Paths.FixturesDir,
            input: new { meetingId = meeting.Id, isDiscovered = true, discoveredInMeetingId = meeting.Id },
            output: new { task.IsDiscovered, task.DiscoveredInMeetingId });

        Assert.True(task.IsDiscovered);
        Assert.Equal(meeting.Id, task.DiscoveredInMeetingId);
    }
}
