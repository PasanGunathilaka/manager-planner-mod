using ExecutivePlanning.Core.Domain;

namespace Baseline.Harness;

/// <summary>
/// Pure-function seam: ProjectSummary.PercentComplete (Services/Reports.cs:63) —
/// TotalTasks == 0 ? 0 : Math.Round(100.0 * Done / TotalTasks, 1). Exercised via
/// GetProjectSummaryAsync since the service is the only producer of a ProjectSummary in this
/// codebase; the property computation itself remains pure (no DB/clock read inside the getter).
/// </summary>
public class PercentCompleteScenarios
{
    [Fact]
    public async Task GM034_PercentComplete_TotalTasksZero_SpecialCase()
    {
        using var t = new TestDb();
        var (svc, _, _, projectId) = await Arrange.ManagerMemberProjectAsync(t);

        var summary = await svc.GetProjectSummaryAsync(projectId);

        FixtureWriter.Write("GM-034", Paths.FixturesDir,
            input: new { totalTasks = 0, doneTasks = 0 },
            output: new { summary.TotalTasks, summary.Done, summary.PercentComplete });

        Assert.Equal(0, summary.PercentComplete); // the explicit TotalTasks == 0 ? 0 : ... branch, not a 0/0 division
    }

    [Fact]
    public async Task GM035_PercentComplete_GenuineZeroPercent_ViaDivision()
    {
        using var t = new TestDb();
        var (svc, _, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        for (var i = 0; i < 3; i++)
            await svc.AddTaskAsync(projectId, $"Task {i}", null, memberId, null);

        var summary = await svc.GetProjectSummaryAsync(projectId);

        FixtureWriter.Write("GM-035", Paths.FixturesDir,
            input: new { totalTasks = 3, doneTasks = 0 },
            output: new { summary.TotalTasks, summary.Done, summary.PercentComplete });

        Assert.Equal(0.0, summary.PercentComplete); // this time via Math.Round(100.0 * 0 / 3, 1), not the special-cased branch
    }

    [Fact]
    public async Task GM036_PercentComplete_HundredPercent_AllDone()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        for (var i = 0; i < 2; i++)
        {
            var task = await svc.AddTaskAsync(projectId, $"Task {i}", null, memberId, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done, managerId);
        }

        var summary = await svc.GetProjectSummaryAsync(projectId);

        FixtureWriter.Write("GM-036", Paths.FixturesDir,
            input: new { totalTasks = 2, doneTasks = 2 },
            output: new { summary.TotalTasks, summary.Done, summary.PercentComplete });

        Assert.Equal(100.0, summary.PercentComplete);
    }

    [Fact]
    public async Task GM037_PercentComplete_RepeatingDecimalRounding_OneOfThree()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var first = await svc.AddTaskAsync(projectId, "Task 0", null, memberId, null);
        await svc.AddTaskAsync(projectId, "Task 1", null, memberId, null);
        await svc.AddTaskAsync(projectId, "Task 2", null, memberId, null);
        await svc.ChangeStatusAsync(first.Id, WorkItemStatus.Done, managerId);

        var summary = await svc.GetProjectSummaryAsync(projectId);

        FixtureWriter.Write("GM-037", Paths.FixturesDir,
            input: new { totalTasks = 3, doneTasks = 1 },
            output: new { summary.TotalTasks, summary.Done, summary.PercentComplete });

        // Math.Round(100.0/3, 1) rounds to exactly one decimal place — 33.3, not 33, not 33.33, not 34.
        Assert.Equal(33.3, summary.PercentComplete);
    }

    [Fact]
    public async Task GM038_PercentComplete_ExactMidpointRounding_ToEven_OneOfEighty()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        WorkItem? first = null;
        for (var i = 0; i < 80; i++)
        {
            var task = await svc.AddTaskAsync(projectId, $"Task {i}", null, memberId, null);
            first ??= task;
        }
        await svc.ChangeStatusAsync(first!.Id, WorkItemStatus.Done, managerId);

        var summary = await svc.GetProjectSummaryAsync(projectId);

        FixtureWriter.Write("GM-038", Paths.FixturesDir,
            input: new { totalTasks = 80, doneTasks = 1 },
            output: new { summary.TotalTasks, summary.Done, summary.PercentComplete });

        // 100.0 * 1 / 80 = 1.25 exactly — a genuine tie at the second decimal place.
        // Math.Round's default MidpointRounding.ToEven (banker's rounding) rounds to the
        // nearest *even* first-decimal digit: 1.2, not 1.3 (which AwayFromZero/naive rounding
        // would produce instead) — this directly resolves CQ-020's midpoint tie-break.
        Assert.Equal(1.2, summary.PercentComplete);
    }
}
