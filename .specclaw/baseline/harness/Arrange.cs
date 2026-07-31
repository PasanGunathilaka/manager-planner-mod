using ExecutivePlanning.Core.Domain;
using ExecutivePlanning.Core.Services;

namespace Baseline.Harness;

/// <summary>
/// Shared arrange helper, mirroring the legacy repo's own
/// ../../../../manager-planner/tests/ExecutivePlanning.Tests/PlanningServiceTests.cs private
/// ArrangeAsync helper exactly (a manager, a team member, and a project owned by the manager),
/// read directly rather than invented, so scenario Facts below do not each reimplement it.
/// </summary>
public static class Arrange
{
    public static async Task<(PlanningService Svc, int ManagerId, int MemberId, int ProjectId)> ManagerMemberProjectAsync(TestDb t)
    {
        var svc = new PlanningService(t.Db);
        var mgr = await svc.AddUserAsync("Manager", "mgr@test", UserRole.Manager);
        var member = await svc.AddUserAsync("Member", "member@test", UserRole.TeamMember);
        var proj = await svc.AddProjectAsync("Proj", "desc", mgr.Id);
        return (svc, mgr.Id, member.Id, proj.Id);
    }
}
