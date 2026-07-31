using ExecutivePlanning.Core.Services;

namespace Baseline.Harness;

/// <summary>
/// Pure-function seam: AccountabilityRow.Verdict (Services/Reports.cs:37-47). Constructed
/// directly — no DB, no clock read inside the getter itself — to pin the five-way precedence
/// order exactly, including the CQ-019/CQ-024-flagged "Overdue (no promise)" mislabeling nuance
/// (GM-010), the single highest-priority verification item in rebuild-backlog.md.
/// </summary>
public class VerdictPrecedenceScenarios
{
    [Fact]
    public void GM008_Verdict_PromiseKeptWinsOutright()
    {
        var row = new AccountabilityRow
        {
            PromiseKept = true,
            PromiseBroken = true, // deliberately also true, to prove precedence, not just that Kept alone works
            IsOverdue = true,
            LatestPromisedDate = new DateTime(2026, 1, 1)
        };

        FixtureWriter.Write("GM-008", Paths.FixturesDir,
            input: new { row.PromiseKept, row.PromiseBroken, row.IsOverdue, latestPromisedDate = row.LatestPromisedDate!.Value.ToString("o") },
            output: new { verdict = row.Verdict });

        Assert.Equal("Kept promise", row.Verdict);
    }

    [Fact]
    public void GM009_Verdict_PromiseBrokenBeatsIsOverdue()
    {
        var row = new AccountabilityRow
        {
            PromiseKept = false,
            PromiseBroken = true,
            IsOverdue = true,
            LatestPromisedDate = new DateTime(2026, 1, 1)
        };

        FixtureWriter.Write("GM-009", Paths.FixturesDir,
            input: new { row.PromiseKept, row.PromiseBroken, row.IsOverdue, latestPromisedDate = row.LatestPromisedDate!.Value.ToString("o") },
            output: new { verdict = row.Verdict });

        Assert.Equal("BROKE promise", row.Verdict);
    }

    [Fact]
    public void GM010_Verdict_OverdueNoPromiseMislabeling_DespitePromiseOnRecord()
    {
        // A promise genuinely exists (LatestPromisedDate has a real, non-null, future value) and
        // is not yet due/broken, but the task's own deadline has separately passed (IsOverdue).
        // Verdict's getter checks IsOverdue before it ever looks at LatestPromisedDate, so the
        // label reads "(no promise)" even though a promise is on record — this is the exact
        // nuance CQ-019/CQ-024 flag as the fixture a rebuild developer would be most tempted to
        // "fix" on sight.
        var row = new AccountabilityRow
        {
            PromiseKept = false,
            PromiseBroken = false,
            IsOverdue = true,
            LatestPromisedDate = new DateTime(2099, 1, 1)
        };

        FixtureWriter.Write("GM-010", Paths.FixturesDir,
            input: new { row.PromiseKept, row.PromiseBroken, row.IsOverdue, latestPromisedDate = row.LatestPromisedDate!.Value.ToString("o") },
            output: new { verdict = row.Verdict });

        Assert.Equal("Overdue (no promise)", row.Verdict);
    }

    [Fact]
    public void GM011_Verdict_PromisePending()
    {
        var row = new AccountabilityRow
        {
            PromiseKept = false,
            PromiseBroken = false,
            IsOverdue = false,
            LatestPromisedDate = new DateTime(2099, 1, 1)
        };

        FixtureWriter.Write("GM-011", Paths.FixturesDir,
            input: new { row.PromiseKept, row.PromiseBroken, row.IsOverdue, latestPromisedDate = row.LatestPromisedDate!.Value.ToString("o") },
            output: new { verdict = row.Verdict });

        Assert.Equal("Promise pending", row.Verdict);
    }

    [Fact]
    public void GM012_Verdict_OnTrack_NoPromiseNothingOverdue()
    {
        var row = new AccountabilityRow(); // all four flags/LatestPromisedDate at their falsy/null defaults

        FixtureWriter.Write("GM-012", Paths.FixturesDir,
            input: new { row.PromiseKept, row.PromiseBroken, row.IsOverdue, latestPromisedDate = (string?)null },
            output: new { verdict = row.Verdict });

        Assert.Equal("On track", row.Verdict);
    }
}
