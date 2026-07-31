using ExecutivePlanning.Core.Services;

namespace Baseline.Harness;

/// <summary>
/// Pure-function seam: PlanningRules' six validators (Services/PlanningValidation.cs:21-75). No
/// DB, no clock, except ValidateNoteDate (GM-006/GM-007), which already accepts an injectable
/// nowUtc override in the legacy source itself — pinning it needs no rebuild-side change at all.
/// </summary>
public class PureFunctionValidatorScenarios
{
    [Fact]
    public void GM001_ProjectName_EmptyRejected_Exact120CharBoundary()
    {
        var whitespace = Capture.Run(() => PlanningRules.ValidateProjectName("   "));
        var exact120 = Capture.Run(() => PlanningRules.ValidateProjectName(new string('a', PlanningRules.MaxProjectName)));
        var over121 = Capture.Run(() => PlanningRules.ValidateProjectName(new string('a', PlanningRules.MaxProjectName + 1)));

        FixtureWriter.Write("GM-001", Paths.FixturesDir,
            input: new { whitespace = "   ", exact120Length = PlanningRules.MaxProjectName, over121Length = PlanningRules.MaxProjectName + 1 },
            output: new { whitespace, exact120, over121 });

        Assert.True(whitespace.Threw);
        Assert.False(exact120.Threw);
        Assert.True(over121.Threw);
    }

    [Fact]
    public void GM002_TaskTitle_EmptyRejected_Exact120CharBoundary()
    {
        var whitespace = Capture.Run(() => PlanningRules.ValidateTaskTitle("   "));
        var exact120 = Capture.Run(() => PlanningRules.ValidateTaskTitle(new string('a', PlanningRules.MaxTaskTitle)));
        var over121 = Capture.Run(() => PlanningRules.ValidateTaskTitle(new string('a', PlanningRules.MaxTaskTitle + 1)));

        FixtureWriter.Write("GM-002", Paths.FixturesDir,
            input: new { whitespace = "   ", exact120Length = PlanningRules.MaxTaskTitle, over121Length = PlanningRules.MaxTaskTitle + 1 },
            output: new { whitespace, exact120, over121 });

        Assert.True(whitespace.Threw);
        Assert.False(exact120.Threw);
        Assert.True(over121.Threw);
    }

    [Fact]
    public void GM003_ObjectiveTitle_EmptyRejected_Exact150CharBoundary()
    {
        var whitespace = Capture.Run(() => PlanningRules.ValidateObjectiveTitle("   "));
        var exact150 = Capture.Run(() => PlanningRules.ValidateObjectiveTitle(new string('a', PlanningRules.MaxObjectiveTitle)));
        var over151 = Capture.Run(() => PlanningRules.ValidateObjectiveTitle(new string('a', PlanningRules.MaxObjectiveTitle + 1)));

        FixtureWriter.Write("GM-003", Paths.FixturesDir,
            input: new { whitespace = "   ", exact150Length = PlanningRules.MaxObjectiveTitle, over151Length = PlanningRules.MaxObjectiveTitle + 1 },
            output: new { whitespace, exact150, over151 });

        Assert.True(whitespace.Threw);
        Assert.False(exact150.Threw);
        Assert.True(over151.Threw);
    }

    [Fact]
    public void GM004_ChecklistLabel_EmptyRejected_Exact300CharBoundary()
    {
        var whitespace = Capture.Run(() => PlanningRules.ValidateChecklistLabel("   "));
        var exact300 = Capture.Run(() => PlanningRules.ValidateChecklistLabel(new string('a', PlanningRules.MaxChecklistLabel)));
        var over301 = Capture.Run(() => PlanningRules.ValidateChecklistLabel(new string('a', PlanningRules.MaxChecklistLabel + 1)));

        FixtureWriter.Write("GM-004", Paths.FixturesDir,
            input: new { whitespace = "   ", exact300Length = PlanningRules.MaxChecklistLabel, over301Length = PlanningRules.MaxChecklistLabel + 1 },
            output: new { whitespace, exact300, over301 });

        Assert.True(whitespace.Threw);
        Assert.False(exact300.Threw);
        Assert.True(over301.Threw);
    }

    [Fact]
    public void GM005_NoteText_EmptyRejected_Exact2000CharBoundary()
    {
        var whitespace = Capture.Run(() => PlanningRules.ValidateNoteText("   "));
        var exact2000 = Capture.Run(() => PlanningRules.ValidateNoteText(new string('a', PlanningRules.MaxNoteText)));
        var over2001 = Capture.Run(() => PlanningRules.ValidateNoteText(new string('a', PlanningRules.MaxNoteText + 1)));

        FixtureWriter.Write("GM-005", Paths.FixturesDir,
            input: new { whitespace = "   ", exact2000Length = PlanningRules.MaxNoteText, over2001Length = PlanningRules.MaxNoteText + 1 },
            output: new { whitespace, exact2000, over2001 });

        Assert.True(whitespace.Threw);
        Assert.False(exact2000.Threw);
        Assert.True(over2001.Threw);
    }

    // GM-006/GM-007 pin "now" via ValidateNoteDate's own injectable nowUtc parameter
    // (PlanningValidation.cs:65) — the cheapest, highest-fidelity capture in the codebase per
    // seams.md. The exact anchor chosen below is arbitrary; any fixed value works since the
    // method is fully pure once nowUtc is supplied.
    private static readonly DateTime PinnedNowUtc = new(2026, 7, 30, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GM006_NoteDate_ExactOneMonthBackBoundary_PinnedNow()
    {
        var earliest = PinnedNowUtc.AddMonths(-1);
        var atEarliest = Capture.Run(() => PlanningRules.ValidateNoteDate(earliest, PinnedNowUtc));
        var oneDayEarlier = Capture.Run(() => PlanningRules.ValidateNoteDate(earliest.AddDays(-1), PinnedNowUtc));

        FixtureWriter.Write("GM-006", Paths.FixturesDir,
            input: new
            {
                nowUtc = PinnedNowUtc.ToString("o"),
                earliest = earliest.ToString("o"),
                oneDayEarlier = earliest.AddDays(-1).ToString("o")
            },
            output: new { atEarliest, oneDayEarlier });

        Assert.False(atEarliest.Threw);
        Assert.True(oneDayEarlier.Threw);
    }

    [Fact]
    public void GM007_NoteDate_TodayAccepted_TomorrowRejected_PinnedNow()
    {
        var today = Capture.Run(() => PlanningRules.ValidateNoteDate(PinnedNowUtc, PinnedNowUtc));
        var tomorrow = Capture.Run(() => PlanningRules.ValidateNoteDate(PinnedNowUtc.AddDays(1), PinnedNowUtc));

        FixtureWriter.Write("GM-007", Paths.FixturesDir,
            input: new
            {
                nowUtc = PinnedNowUtc.ToString("o"),
                today = PinnedNowUtc.ToString("o"),
                tomorrow = PinnedNowUtc.AddDays(1).ToString("o")
            },
            output: new { today, tomorrow });

        Assert.False(today.Threw);
        Assert.True(tomorrow.Threw);
    }
}
