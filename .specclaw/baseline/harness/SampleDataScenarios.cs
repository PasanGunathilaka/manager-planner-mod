using ExecutivePlanning.Core.Data;
using ExecutivePlanning.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Baseline.Harness;

/// <summary>
/// Stateful service boundary: PlanningService.LoadSampleDataIfEmpty (which wraps
/// DbSeeder.SeedIfEmpty) and DbSeeder.ResetToSampleData. Deliberately restricted to structural
/// counts/existence, never any date-derived Verdict/Overdue outcome — every seeded
/// deadline/meeting-date/note-date/promised-date in DbSeeder is DateTime.UtcNow-relative with no
/// injectable override anywhere in its public surface (seams.md CB-5).
/// </summary>
public class SampleDataScenarios
{
    [Fact]
    public async Task GM041_LoadSampleDataIfEmpty_PopulatesDocumentedStructuralShape()
    {
        using var t = new TestDb();
        var svc = new PlanningService(t.Db);

        var seeded = svc.LoadSampleDataIfEmpty();

        var shape = new
        {
            users = await t.Db.Users.CountAsync(),
            projects = await t.Db.Projects.CountAsync(),
            anyDiscoveredWorkItem = await t.Db.WorkItems.AnyAsync(w => w.IsDiscovered),
            anyPromiseNote = await t.Db.ProgressNotes.AnyAsync(n => n.IsPromise),
            anyObjective = await t.Db.Objectives.AnyAsync(),
            anyNestedChecklistItem = await t.Db.ChecklistItems.AnyAsync(c => c.ParentId != null),
            anyTaskOwner = await t.Db.TaskOwners.AnyAsync()
        };

        FixtureWriter.Write("GM-041", Paths.FixturesDir,
            input: new { startedFromEmptyDatabase = true },
            output: new { seeded, shape });

        Assert.True(seeded);
        Assert.Equal(6, shape.users);
        Assert.Equal(3, shape.projects);
        Assert.True(shape.anyDiscoveredWorkItem);
        Assert.True(shape.anyPromiseNote);
        Assert.True(shape.anyObjective);
        Assert.True(shape.anyNestedChecklistItem); // nesting exists
        Assert.True(shape.anyTaskOwner);
    }

    [Fact]
    public async Task GM042_LoadSampleDataIfEmpty_IsIdempotent()
    {
        using var t = new TestDb();
        var svc = new PlanningService(t.Db);
        svc.LoadSampleDataIfEmpty();
        var countAfterFirstSeed = await t.Db.Users.CountAsync();

        var seededSecondTime = svc.LoadSampleDataIfEmpty();
        var countAfterSecondCall = await t.Db.Users.CountAsync();

        FixtureWriter.Write("GM-042", Paths.FixturesDir,
            input: new { countAfterFirstSeed },
            output: new { seededSecondTime, countAfterSecondCall });

        Assert.False(seededSecondTime); // no duplicate seeding occurs
        Assert.Equal(6, countAfterSecondCall);
    }

    [Fact]
    public async Task GM043_ResetToSampleData_WipesEditsAndRestoresFreshSampleCounts()
    {
        using var t = new TestDb();
        DbSeeder.SeedIfEmpty(t.Db);
        var svc = new PlanningService(t.Db);

        // user edits the data
        var firstProject = await t.Db.Projects.FirstAsync();
        await svc.AddTaskAsync(firstProject.Id, "my extra task", null, null, null);

        DbSeeder.ResetToSampleData(t.Db);

        var shape = new
        {
            users = await t.Db.Users.CountAsync(),
            projects = await t.Db.Projects.CountAsync(),
            extraTaskStillExists = await t.Db.WorkItems.AnyAsync(w => w.Title == "my extra task")
        };

        FixtureWriter.Write("GM-043", Paths.FixturesDir,
            input: new { extraTaskTitle = "my extra task" },
            output: shape);

        Assert.Equal(6, shape.users);  // fresh sample restored
        Assert.Equal(3, shape.projects);
        Assert.False(shape.extraTaskStillExists); // the edit is gone
    }
}
