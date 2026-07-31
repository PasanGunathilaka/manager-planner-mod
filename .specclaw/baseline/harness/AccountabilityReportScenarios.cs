using ExecutivePlanning.Core.Domain;

namespace Baseline.Harness;

/// <summary>
/// Stateful service boundary: PlanningService.GetAccountabilityReportAsync
/// (Services/PlanningService.cs:269-330). Every date here is expressed as an offset from
/// FixtureWriter.AnchorDate (seams.md CB-1/CB-2's Option 3 mitigation) because
/// ChangeStatusAsync's CompletedUtc write and this method's own "now" read are both unguarded
/// DateTime.UtcNow calls with no injectable override anywhere in the legacy source — there is
/// nothing this harness can inject into the legacy code itself; it can only anchor the fixture's
/// *inputs* to a single captured "today" so the boundary under test (same-day vs. one-day-off)
/// holds regardless of which calendar day the fixture is replayed on.
/// </summary>
public class AccountabilityReportScenarios
{
    [Fact]
    public async Task GM013_PromiseKept_ExactEqualityBoundary()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var anchor = FixtureWriter.AnchorDate;
        var promisedDate = anchor; // CompletedUtc (set below) is expected to land on this same calendar date

        var task = await svc.AddTaskAsync(projectId, "Cutover", null, memberId, null);
        await svc.AddNoteAsync(task.Id, "will finish today", managerId, isPromise: true, promisedDate: promisedDate);
        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done, managerId);

        var report = await svc.GetAccountabilityReportAsync(projectId);
        var row = Assert.Single(report);

        FixtureWriter.Write("GM-013", Paths.FixturesDir,
            input: new { anchorDate = anchor.ToString("yyyy-MM-dd"), promisedDate = promisedDate.ToString("o") },
            output: new
            {
                row.PromiseKept,
                row.PromiseBroken,
                row.IsOverdue,
                row.Verdict,
                completedUtc = row.CompletedUtc?.ToString("o")
            },
            normalizedFields: new[] { "output.completedUtc" }); // exact instant not asserted, only its date relationship to promisedDate

        Assert.True(row.PromiseKept);
        Assert.False(row.PromiseBroken);
        Assert.Equal("Kept promise", row.Verdict);
    }

    [Fact]
    public async Task GM014_PromiseBroken_ExactBoundary_DoneButOneDayLate()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var anchor = FixtureWriter.AnchorDate;
        var promisedDate = anchor.AddDays(-1); // CompletedUtc (set below) lands one day after this

        var task = await svc.AddTaskAsync(projectId, "Cutover", null, memberId, null);
        await svc.AddNoteAsync(task.Id, "will finish yesterday", managerId, isPromise: true, promisedDate: promisedDate);
        await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done, managerId);

        var report = await svc.GetAccountabilityReportAsync(projectId);
        var row = Assert.Single(report);

        FixtureWriter.Write("GM-014", Paths.FixturesDir,
            input: new { anchorDate = anchor.ToString("yyyy-MM-dd"), promisedDate = promisedDate.ToString("o") },
            output: new
            {
                row.PromiseKept,
                row.PromiseBroken,
                row.IsOverdue,
                row.Verdict,
                completedUtc = row.CompletedUtc?.ToString("o")
            },
            normalizedFields: new[] { "output.completedUtc" });

        Assert.False(row.PromiseKept);
        Assert.True(row.PromiseBroken);
        Assert.Equal("BROKE promise", row.Verdict);
    }

    [Fact]
    public async Task GM015_NotDone_PromiseDueExactlyToday_IsNotYetBroken()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var anchor = FixtureWriter.AnchorDate;
        var promisedDate = anchor; // strict "<" comparison in the source: same-day promise is not yet broken

        var task = await svc.AddTaskAsync(projectId, "Cutover", null, memberId, null);
        await svc.AddNoteAsync(task.Id, "will finish today", managerId, isPromise: true, promisedDate: promisedDate);
        // task deliberately stays NotStarted

        var report = await svc.GetAccountabilityReportAsync(projectId);
        var row = Assert.Single(report);

        FixtureWriter.Write("GM-015", Paths.FixturesDir,
            input: new { anchorDate = anchor.ToString("yyyy-MM-dd"), promisedDate = promisedDate.ToString("o") },
            output: new { row.PromiseKept, row.PromiseBroken, row.IsOverdue, row.Verdict });

        Assert.False(row.PromiseBroken);
    }

    [Fact]
    public async Task GM016_NotDone_PromiseOneDayOverdue_IsBroken()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var anchor = FixtureWriter.AnchorDate;
        var promisedDate = anchor.AddDays(-1);

        var task = await svc.AddTaskAsync(projectId, "Cutover", null, memberId, null);
        await svc.AddNoteAsync(task.Id, "will finish yesterday", managerId, isPromise: true, promisedDate: promisedDate);

        var report = await svc.GetAccountabilityReportAsync(projectId);
        var row = Assert.Single(report);

        FixtureWriter.Write("GM-016", Paths.FixturesDir,
            input: new { anchorDate = anchor.ToString("yyyy-MM-dd"), promisedDate = promisedDate.ToString("o") },
            output: new { row.PromiseKept, row.PromiseBroken, row.IsOverdue, row.Verdict });

        Assert.True(row.PromiseBroken);
        Assert.Equal("BROKE promise", row.Verdict);
    }

    [Fact]
    public async Task GM017_LatestPromise_SupersedesOlderOne()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var anchor = FixtureWriter.AnchorDate;
        var task = await svc.AddTaskAsync(projectId, "API", null, memberId, null);

        // Inserted directly against PlanningDbContext (not AddNoteAsync) so CreatedUtc can be
        // controlled explicitly, mirroring Accountability_uses_latest_promise in the legacy
        // test suite exactly.
        var olderPromisedDate = anchor.AddDays(-3); // would compute as broken if it were used
        t.Db.ProgressNotes.Add(new ProgressNote
        {
            WorkItemId = task.Id,
            AuthorId = managerId,
            Text = "old promise",
            IsPromise = true,
            PromisedDate = olderPromisedDate,
            CreatedUtc = anchor.AddDays(-5)
        });
        await t.Db.SaveChangesAsync();

        var newerPromisedDate = anchor.AddDays(4); // not yet due
        await svc.AddNoteAsync(task.Id, "revised promise", managerId, isPromise: true, promisedDate: newerPromisedDate);

        var report = await svc.GetAccountabilityReportAsync(projectId);
        var row = Assert.Single(report);

        FixtureWriter.Write("GM-017", Paths.FixturesDir,
            input: new
            {
                anchorDate = anchor.ToString("yyyy-MM-dd"),
                olderPromisedDate = olderPromisedDate.ToString("o"),
                newerPromisedDate = newerPromisedDate.ToString("o")
            },
            output: new
            {
                latestPromisedDate = row.LatestPromisedDate?.ToString("o"),
                row.PromiseBroken,
                row.Verdict
            });

        Assert.Equal(newerPromisedDate.Date, row.LatestPromisedDate!.Value.Date);
        Assert.False(row.PromiseBroken); // older, would-be-broken promise is fully superseded, not merged/averaged
    }

    [Fact]
    public async Task GM018_SortOrder_GenuineTie_UndocumentedTieBreak()
    {
        using var t = new TestDb();
        var (svc, managerId, memberId, projectId) = await Arrange.ManagerMemberProjectAsync(t);
        var anchor = FixtureWriter.AnchorDate;
        var sharedDeadline = anchor.AddDays(-2); // overdue for both
        var sharedPromisedDate = anchor.AddDays(-2); // broken for both

        var taskA = await svc.AddTaskAsync(projectId, "Task A", null, memberId, sharedDeadline);
        await svc.AddNoteAsync(taskA.Id, "A's promise", managerId, isPromise: true, promisedDate: sharedPromisedDate);

        var taskB = await svc.AddTaskAsync(projectId, "Task B", null, memberId, sharedDeadline);
        await svc.AddNoteAsync(taskB.Id, "B's promise", managerId, isPromise: true, promisedDate: sharedPromisedDate);

        var report = await svc.GetAccountabilityReportAsync(projectId);
        Assert.Equal(2, report.Count);
        Assert.True(report[0].PromiseBroken && report[0].IsOverdue);
        Assert.True(report[1].PromiseBroken && report[1].IsOverdue);

        // Deliberately NOT asserting which task comes first — recording whatever order the
        // legacy system's real SQLite/EF Core execution actually produces for this specific tie
        // shape IS the golden master CQ-024 asks for regarding this undocumented tie-break
        // (seams.md CB-4). Do not "correct" this on replay.
        FixtureWriter.Write("GM-018", Paths.FixturesDir,
            input: new
            {
                anchorDate = anchor.ToString("yyyy-MM-dd"),
                sharedDeadline = sharedDeadline.ToString("o"),
                sharedPromisedDate = sharedPromisedDate.ToString("o"),
                taskAId = taskA.Id,
                taskBId = taskB.Id,
                taskATitle = "Task A",
                taskBTitle = "Task B"
            },
            output: new
            {
                order = report.Select(r => r.TaskTitle).ToArray(),
                workItemIdOrder = report.Select(r => r.WorkItemId).ToArray()
            });
    }
}
