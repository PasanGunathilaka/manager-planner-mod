using System.Globalization;
using System.Text.Json;
using ManagerPlanner.Core.Data;
using ManagerPlanner.Core.Domain;
using ManagerPlanner.Core.Services;
using ManagerPlanner.Core.Validation;
using Microsoft.EntityFrameworkCore;

namespace ParityHarness;

public record CaseResult(
    string CaseId,
    object? Output,
    string OutputType, // "value" | "exception" | "not_implemented"
    string? ExceptionMessage,
    string? ExceptionType,
    string? InnerExceptionMessage);

public static class Program
{
    private static readonly List<CaseResult> Results = new();

    private static string Rep(int n) => new string('a', n);

    private static DateTime D(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture,
        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static async Task Exec(string id, Func<Task<object?>> body)
    {
        try
        {
            var val = await body();
            Results.Add(new CaseResult(id, val, "value", null, null, null));
        }
        catch (Exception ex)
        {
            Results.Add(new CaseResult(id, null, "exception", ex.Message, ex.GetType().FullName, ex.InnerException?.Message));
        }
    }

    private static Task ExecValidator(string id, Action act) => Exec(id, () =>
    {
        act();
        return Task.FromResult<object?>("no exception (validation passed)");
    });

    private static void NotImplemented(string id) =>
        Results.Add(new CaseResult(id, null, "not_implemented", null, null, null));

    public static async Task Main()
    {
        // ================= MOD01: PlanningRules (pure validators, no DB) =================
        var mod01 = new (string Id, Action Act)[]
        {
            ("MOD01-C001", () => PlanningRules.ValidateProjectName(null)),
            ("MOD01-C002", () => PlanningRules.ValidateProjectName("")),
            ("MOD01-C003", () => PlanningRules.ValidateProjectName("   ")),
            ("MOD01-C004", () => PlanningRules.ValidateProjectName("a")),
            ("MOD01-C005", () => PlanningRules.ValidateProjectName(Rep(120))),
            ("MOD01-C006", () => PlanningRules.ValidateProjectName(Rep(119))),
            ("MOD01-C007", () => PlanningRules.ValidateProjectName(Rep(121))),
            ("MOD01-C008", () => PlanningRules.ValidateProjectName(" " + Rep(120) + " ")),
            ("MOD01-C009", () => PlanningRules.ValidateTaskTitle(null)),
            ("MOD01-C010", () => PlanningRules.ValidateTaskTitle("")),
            ("MOD01-C011", () => PlanningRules.ValidateTaskTitle("   ")),
            ("MOD01-C012", () => PlanningRules.ValidateTaskTitle("a")),
            ("MOD01-C013", () => PlanningRules.ValidateTaskTitle(Rep(120))),
            ("MOD01-C014", () => PlanningRules.ValidateTaskTitle(Rep(119))),
            ("MOD01-C015", () => PlanningRules.ValidateTaskTitle(Rep(121))),
            ("MOD01-C016", () => PlanningRules.ValidateTaskTitle(" " + Rep(120) + " ")),
            ("MOD01-C017", () => PlanningRules.ValidateObjectiveTitle(null)),
            ("MOD01-C018", () => PlanningRules.ValidateObjectiveTitle("")),
            ("MOD01-C019", () => PlanningRules.ValidateObjectiveTitle("   ")),
            ("MOD01-C020", () => PlanningRules.ValidateObjectiveTitle("a")),
            ("MOD01-C021", () => PlanningRules.ValidateObjectiveTitle(Rep(150))),
            ("MOD01-C022", () => PlanningRules.ValidateObjectiveTitle(Rep(149))),
            ("MOD01-C023", () => PlanningRules.ValidateObjectiveTitle(Rep(151))),
            ("MOD01-C024", () => PlanningRules.ValidateObjectiveTitle(" " + Rep(150) + " ")),
            ("MOD01-C025", () => PlanningRules.ValidateChecklistLabel(null)),
            ("MOD01-C026", () => PlanningRules.ValidateChecklistLabel("")),
            ("MOD01-C027", () => PlanningRules.ValidateChecklistLabel("   ")),
            ("MOD01-C028", () => PlanningRules.ValidateChecklistLabel("a")),
            ("MOD01-C029", () => PlanningRules.ValidateChecklistLabel(Rep(300))),
            ("MOD01-C030", () => PlanningRules.ValidateChecklistLabel(Rep(299))),
            ("MOD01-C031", () => PlanningRules.ValidateChecklistLabel(Rep(301))),
            ("MOD01-C032", () => PlanningRules.ValidateChecklistLabel(" " + Rep(300) + " ")),
            ("MOD01-C033", () => PlanningRules.ValidateNoteText(null)),
            ("MOD01-C034", () => PlanningRules.ValidateNoteText("")),
            ("MOD01-C035", () => PlanningRules.ValidateNoteText("   ")),
            ("MOD01-C036", () => PlanningRules.ValidateNoteText("a")),
            ("MOD01-C037", () => PlanningRules.ValidateNoteText(Rep(2000))),
            ("MOD01-C038", () => PlanningRules.ValidateNoteText(Rep(1999))),
            ("MOD01-C039", () => PlanningRules.ValidateNoteText(Rep(2001))),
            ("MOD01-C040", () => PlanningRules.ValidateNoteText(" " + Rep(2000) + " ")),
            ("MOD01-C041", () => PlanningRules.ValidateNoteDate(D("0001-01-01T00:00:00Z"), null)),
            ("MOD01-C042", () => PlanningRules.ValidateNoteDate(D("2026-06-30T00:00:00Z"), D("2026-07-30T00:00:00Z"))),
            ("MOD01-C043", () => PlanningRules.ValidateNoteDate(D("2026-06-29T00:00:00Z"), D("2026-07-30T00:00:00Z"))),
            ("MOD01-C044", () => PlanningRules.ValidateNoteDate(D("2026-07-30T00:00:00Z"), D("2026-07-30T00:00:00Z"))),
            ("MOD01-C045", () => PlanningRules.ValidateNoteDate(D("2026-07-31T00:00:00Z"), D("2026-07-30T00:00:00Z"))),
            ("MOD01-C046", () => PlanningRules.ValidateNoteDate(D("2024-02-29T00:00:00Z"), D("2024-03-31T00:00:00Z"))),
            ("MOD01-C047", () => PlanningRules.ValidateNoteDate(D("2024-02-28T00:00:00Z"), D("2024-03-31T00:00:00Z"))),
            ("MOD01-C048", () => PlanningRules.ValidateNoteDate(D("2026-07-30T23:59:59Z"), D("2026-07-30T15:00:00Z"))),
        };
        foreach (var (id, act) in mod01) await ExecValidator(id, act);

        // ================= MOD02: Reports.cs =================
        // AccountabilityRow class does not exist anywhere in ManagerPlanner.Core (confirmed by
        // grep) -- MOD02-C001..C008 (AccountabilityRow.Verdict) have no modern equivalent.
        foreach (var id in new[] { "C001", "C002", "C003", "C004", "C005", "C006", "C007", "C008" })
            NotImplemented("MOD02-" + id);

        var mod02 = new (string Id, int Total, int Done)[]
        {
            ("MOD02-C009", 0, 0),
            ("MOD02-C010", 1, 0),
            ("MOD02-C011", 1, 1),
            ("MOD02-C012", 3, 1),
            ("MOD02-C013", 80, 1),
            ("MOD02-C014", 400, 7),
            ("MOD02-C015", -1, 0),
            ("MOD02-C016", 2147483647, 2147483647),
            ("MOD02-C017", 1, -2147483648),
        };
        foreach (var (id, total, done) in mod02)
        {
            await Exec(id, () =>
            {
                var ps = new ProjectSummary { TotalTasks = total, Done = done };
                var v = ps.PercentComplete;
                return Task.FromResult<object?>(new { Value = v, IsNegativeZero = v == 0.0 && double.IsNegative(v) });
            });
        }

        // ================= MOD03: PlanningService (methods that actually exist) =================
        // Not-implemented: HasAnyData, LoadSampleDataIfEmpty, ResetSampleData, GetUsersAsync,
        // AddUserAsync, DeleteProjectAsync, DeleteTaskAsync, GetTasksForProjectAsync, GetTaskAsync,
        // GetObjectivesForProjectAsync, AddChecklistItemAsync,
        // SetOwnersAsync, GetMeetingsForProjectAsync, AddMeetingAsync, AddNoteAsync,
        // GetNotesForTaskAsync, GetAccountabilityReportAsync, GetAccountabilityForAllProjectsAsync
        // -- none of these exist anywhere in ManagerPlanner.Core.Services.PlanningService (confirmed
        // by grep across the whole project).
        // ToggleChecklistItemAsync now exists (added by nested-checklist-items-and-grid-status-badges,
        // 2026-07-31) -- dispatched for real below, C042-C044.
        foreach (var id in new[]
                 {
                     "C001", "C002", "C003", "C004", "C005", "C006", "C007", "C008",
                     "C011", "C012", "C013", "C014",
                     "C020", "C021", "C022", "C023", "C024", "C025", "C026", "C027",
                     "C032", "C033",
                     "C039", "C040", "C041", "C045", "C046", "C047",
                     "C056", "C057", "C058", "C059", "C060", "C061", "C062", "C063", "C064", "C065",
                     "C066", "C067",
                     "C068", "C069", "C070", "C071", "C072", "C073", "C074", "C075", "C076", "C077",
                     "C078", "C079", "C080", "C081", "C082", "C083", "C084", "C085"
                 })
            NotImplemented("MOD03-" + id);

        // ---- C009/C010: GetTeamMembersAsync ----
        await Exec("MOD03-C009", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            using (var db = t.NewContext())
            {
                db.Users.AddRange(
                    new User { FullName = "Manager", Email = "mgr@t.local", Role = UserRole.Manager, IsActive = true },
                    new User { FullName = "Active Member", Email = "am@t.local", Role = UserRole.TeamMember, IsActive = true },
                    new User { FullName = "Inactive Member", Email = "im@t.local", Role = UserRole.TeamMember, IsActive = false });
                await db.SaveChangesAsync();
            }
            var members = await svc.GetTeamMembersAsync();
            return members.Select(m => m.FullName).ToArray();
        });

        await Exec("MOD03-C010", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            using (var db = t.NewContext())
            {
                db.Users.Add(new User { FullName = "Mgr", Email = "mgr2@t.local", Role = UserRole.Manager });
                await db.SaveChangesAsync();
            }
            var members = await svc.GetTeamMembersAsync();
            return members.Count;
        });

        // ---- C015/C016: GetProjectsAsync ----
        await Exec("MOD03-C015", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            int ownerId;
            using (var db = t.NewContext())
            {
                var owner = new User { FullName = "Owner", Email = "o1@t.local", Role = UserRole.Manager };
                db.Users.Add(owner);
                await db.SaveChangesAsync();
                ownerId = owner.Id;
            }
            var p1 = await svc.AddProjectAsync("Proj One", null, ownerId);
            await Task.Delay(15);
            var p2 = await svc.AddProjectAsync("Proj Two", null, ownerId);
            var list = await svc.GetProjectsAsync();
            return list.Select(p => p.Name).ToArray();
        });

        await Exec("MOD03-C016", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var list = await svc.GetProjectsAsync();
            return list.Count;
        });

        // ---- C017/C018/C019: AddProjectAsync ----
        await Exec("MOD03-C017", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            int ownerId = await SeedManager(t, "o2@t.local");
            var p = await svc.AddProjectAsync("  Padded Name  ", null, ownerId);
            return p.Name;
        });

        await Exec("MOD03-C018", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var p = await svc.AddProjectAsync("Orphan Project", null, 999999);
            return p.Name; // unreachable if it throws
        });

        await Exec("MOD03-C019", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            int ownerId = await SeedManager(t, "o3@t.local");
            var p = await svc.AddProjectAsync("No Desc", null, ownerId);
            return new { DescriptionIsNull = p.Description == null };
        });

        // ---- C028/C029/C030/C031: AddTaskAsync ----
        await Exec("MOD03-C028", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o4@t.local");
            var task = await svc.AddTaskAsync(projectId, "Minimal", null, null, null);
            return new
            {
                AssigneeId = task.AssigneeId,
                Deadline = task.Deadline,
                ObjectiveId = task.ObjectiveId,
                IsDiscovered = task.IsDiscovered,
                DiscoveredInMeetingId = task.DiscoveredInMeetingId,
                Description = task.Description
            };
        });

        await Exec("MOD03-C029", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o5@t.local");
            var task = await svc.AddTaskAsync(projectId, "Discovered", null, null, null, isDiscovered: true);
            return new { IsDiscovered = task.IsDiscovered, DiscoveredInMeetingId = task.DiscoveredInMeetingId };
        });

        await Exec("MOD03-C030", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o6@t.local");
            var task = await svc.AddTaskAsync(projectId, "Bad Assignee", null, 999999, null);
            return task.Id;
        });

        await Exec("MOD03-C031", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o7@t.local");
            var task = await svc.AddTaskAsync(projectId, "  Padded Title  ", null, null, null);
            return task.Title;
        });

        // ---- C034/C035/C036: AddObjectiveAsync ----
        await Exec("MOD03-C034", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o8@t.local");
            var obj = await svc.AddObjectiveAsync(projectId, "First");
            return obj.SortOrder;
        });

        await Exec("MOD03-C035", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o9@t.local");
            await svc.AddObjectiveAsync(projectId, "First");
            var second = await svc.AddObjectiveAsync(projectId, "Second");
            return second.SortOrder;
        });

        await Exec("MOD03-C036", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId1) = await SeedManagerAndProject(t, "o10@t.local");
            await svc.AddObjectiveAsync(projectId1, "First");
            int projectId2;
            using (var db = t.NewContext())
            {
                var proj2 = new Project { Name = "Other Project", OwnerId = ownerId };
                db.Projects.Add(proj2);
                await db.SaveChangesAsync();
                projectId2 = proj2.Id;
            }
            var firstInOther = await svc.AddObjectiveAsync(projectId2, "First-in-other");
            return firstInOther.SortOrder;
        });

        // ---- C037/C038: GetPlannerForProjectAsync ----
        await Exec("MOD03-C037", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o11@t.local");
            var objective = await svc.AddObjectiveAsync(projectId, "Obj1");
            var now = DateTime.UtcNow;
            await svc.AddTaskAsync(projectId, "Alpha", null, null, now.AddDays(10), objectiveId: objective.Id);
            await svc.AddTaskAsync(projectId, "Beta", null, null, now.AddDays(1), objectiveId: objective.Id);
            var planner = await svc.GetPlannerForProjectAsync(projectId);
            return planner.SelectMany(o => o.Tasks).Select(x => x.Title).ToArray();
        });

        await Exec("MOD03-C038", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "o12@t.local");
            var objective = await svc.AddObjectiveAsync(projectId, "Obj1");
            var task = await svc.AddTaskAsync(projectId, "T1", null, null, null, objectiveId: objective.Id);
            using (var db = t.NewContext())
            {
                db.ChecklistItems.AddRange(
                    new ChecklistItem { WorkItemId = task.Id, Label = "ZZZ", SortOrder = 5 },
                    new ChecklistItem { WorkItemId = task.Id, Label = "AAA", SortOrder = 0 });
                await db.SaveChangesAsync();
            }
            var planner = await svc.GetPlannerForProjectAsync(projectId);
            return planner.SelectMany(o => o.Tasks).SelectMany(x => x.Checklist).Select(c => c.Label).ToArray();
        });

        // ---- C048-C055: ChangeStatusAsync ----
        await Exec("MOD03-C048", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            int changerId = await SeedManager(t, "chg1@t.local");
            await svc.ChangeStatusAsync(999999, WorkItemStatus.Done, changerId);
            return "unreachable";
        });

        await Exec("MOD03-C049", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chg2@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.NotStarted, ownerId);
            using var db = t.NewContext();
            var reloaded = await db.WorkItems.Include(x => x.StatusHistory).FirstAsync(x => x.Id == task.Id);
            return new { StatusHistoryCount = reloaded.StatusHistory.Count, Status = reloaded.Status.ToString() };
        });

        await Exec("MOD03-C050", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chg3@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done, ownerId);
            using var db = t.NewContext();
            var reloaded = await db.WorkItems.Include(x => x.StatusHistory).FirstAsync(x => x.Id == task.Id);
            return new
            {
                Status = reloaded.Status.ToString(),
                CompletedUtcIsNull = reloaded.CompletedUtc == null,
                StatusHistoryCount = reloaded.StatusHistory.Count
            };
        });

        await Exec("MOD03-C051", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chg4@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done, ownerId);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, ownerId);
            using var db = t.NewContext();
            var reloaded = await db.WorkItems.Include(x => x.StatusHistory).FirstAsync(x => x.Id == task.Id);
            return new
            {
                Status = reloaded.Status.ToString(),
                CompletedUtcIsNull = reloaded.CompletedUtc == null,
                StatusHistoryCount = reloaded.StatusHistory.Count
            };
        });

        await Exec("MOD03-C052", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chg5@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Blocked, ownerId);
            using var db = t.NewContext();
            var reloaded = await db.WorkItems.FirstAsync(x => x.Id == task.Id);
            return new { Status = reloaded.Status.ToString(), CompletedUtcIsNull = reloaded.CompletedUtc == null };
        });

        await Exec("MOD03-C053", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chg6@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, ownerId);
            using var db = t.NewContext();
            var change = await db.StatusChanges.FirstAsync(x => x.WorkItemId == task.Id);
            return new { ReasonIsNull = change.Reason == null };
        });

        await Exec("MOD03-C054", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chg7@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Blocked, ownerId, "Waiting on vendor");
            using var db = t.NewContext();
            var change = await db.StatusChanges.FirstAsync(x => x.WorkItemId == task.Id);
            return change.Reason;
        });

        await Exec("MOD03-C055", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chg8@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            await svc.ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, 999999);
            return "unreachable";
        });

        // ---- C042-C044: ToggleChecklistItemAsync ----
        await Exec("MOD03-C042", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            await svc.ToggleChecklistItemAsync(999999, true);
            return "unreachable";
        });

        await Exec("MOD03-C043", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chk1@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            int itemId;
            using (var db = t.NewContext())
            {
                var item = new ChecklistItem { WorkItemId = task.Id, Label = "Step 1", IsDone = false, SortOrder = 0 };
                db.ChecklistItems.Add(item);
                await db.SaveChangesAsync();
                itemId = item.Id;
            }
            await svc.ToggleChecklistItemAsync(itemId, true);
            using var db2 = t.NewContext();
            var reloaded = await db2.ChecklistItems.FirstAsync(c => c.Id == itemId);
            return new { IsDone = reloaded.IsDone, CompletedUtcIsNull = reloaded.CompletedUtc == null };
        });

        await Exec("MOD03-C044", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "chk2@t.local");
            var task = await svc.AddTaskAsync(projectId, "T", null, null, null);
            int itemId;
            using (var db = t.NewContext())
            {
                var item = new ChecklistItem { WorkItemId = task.Id, Label = "Step 1", IsDone = false, SortOrder = 0 };
                db.ChecklistItems.Add(item);
                await db.SaveChangesAsync();
                itemId = item.Id;
            }
            await svc.ToggleChecklistItemAsync(itemId, true);
            await svc.ToggleChecklistItemAsync(itemId, false);
            using var db2 = t.NewContext();
            var reloaded = await db2.ChecklistItems.FirstAsync(c => c.Id == itemId);
            return new { IsDone = reloaded.IsDone, CompletedUtcIsNull = reloaded.CompletedUtc == null };
        });

        // ---- C086-C090: GetProjectSummaryAsync ----
        await Exec("MOD03-C086", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "sum1@t.local");
            var s = await svc.GetProjectSummaryAsync(projectId);
            return new { s.TotalTasks, s.Done, s.PercentComplete };
        });

        await Exec("MOD03-C087", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "sum2@t.local");
            using (var db = t.NewContext())
            {
                db.WorkItems.AddRange(
                    new WorkItem { ProjectId = projectId, Title = "T-NotStarted", Status = WorkItemStatus.NotStarted },
                    new WorkItem { ProjectId = projectId, Title = "T-InProgress", Status = WorkItemStatus.InProgress },
                    new WorkItem { ProjectId = projectId, Title = "T-Blocked", Status = WorkItemStatus.Blocked },
                    new WorkItem { ProjectId = projectId, Title = "T-Done", Status = WorkItemStatus.Done });
                await db.SaveChangesAsync();
            }
            var s = await svc.GetProjectSummaryAsync(projectId);
            return new { s.TotalTasks, s.Done, s.InProgress, s.Blocked, s.NotStarted, s.PercentComplete };
        });

        await Exec("MOD03-C088", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "sum3@t.local");
            var past = DateTime.UtcNow.AddDays(-3);
            using (var db = t.NewContext())
            {
                db.WorkItems.AddRange(
                    new WorkItem { ProjectId = projectId, Title = "Done-past", Status = WorkItemStatus.Done, Deadline = past },
                    new WorkItem { ProjectId = projectId, Title = "NotStarted-past", Status = WorkItemStatus.NotStarted, Deadline = past });
                await db.SaveChangesAsync();
            }
            var s = await svc.GetProjectSummaryAsync(projectId);
            return s.Overdue;
        });

        await Exec("MOD03-C089", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var (ownerId, projectId) = await SeedManagerAndProject(t, "sum4@t.local");
            using (var db = t.NewContext())
            {
                db.WorkItems.Add(new WorkItem { ProjectId = projectId, Title = "NullDeadline", Status = WorkItemStatus.NotStarted, Deadline = null });
                await db.SaveChangesAsync();
            }
            var s = await svc.GetProjectSummaryAsync(projectId);
            return s.Overdue;
        });

        await Exec("MOD03-C090", async () =>
        {
            using var t = new TestDb();
            var svc = new PlanningService(t);
            var s = await svc.GetProjectSummaryAsync(999999);
            return new { ProjectNameIsEmpty = s.ProjectName == string.Empty, s.TotalTasks };
        });

        // ================= MOD04: PlanningDbContext cascade/SetNull/Restrict =================
        await Exec("MOD04-C001", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d1@t.local");
            using var db = t.NewContext();
            var objective = new Objective { ProjectId = projectId, Title = "Obj" };
            db.Objectives.Add(objective);
            await db.SaveChangesAsync();
            var task = new WorkItem { ProjectId = projectId, Title = "T", ObjectiveId = objective.Id };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();

            db.Objectives.Remove(objective);
            await db.SaveChangesAsync();

            var reloaded = await db.WorkItems.FindAsync(task.Id);
            return new { WorkItemSurvived = reloaded != null, ObjectiveIdAfter = reloaded?.ObjectiveId };
        });

        await Exec("MOD04-C002", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d2@t.local");
            var authorId = await SeedTeamMember(t, "d2b@t.local");
            using var db = t.NewContext();
            var meeting = new Meeting { ProjectId = projectId, Title = "M" };
            db.Meetings.Add(meeting);
            await db.SaveChangesAsync();
            var task = new WorkItem { ProjectId = projectId, Title = "T", DiscoveredInMeetingId = meeting.Id };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();
            var note = new ProgressNote { WorkItemId = task.Id, Text = "n", AuthorId = authorId, MeetingId = meeting.Id };
            db.ProgressNotes.Add(note);
            await db.SaveChangesAsync();

            db.Meetings.Remove(meeting);
            await db.SaveChangesAsync();

            var taskAfter = await db.WorkItems.FindAsync(task.Id);
            var noteAfter = await db.ProgressNotes.FindAsync(note.Id);
            return new { TaskDiscoveredInMeetingIdAfter = taskAfter?.DiscoveredInMeetingId, NoteMeetingIdAfter = noteAfter?.MeetingId };
        });

        await Exec("MOD04-C003", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d3@t.local");
            var assigneeId = await SeedTeamMember(t, "d3b@t.local");
            using var db = t.NewContext();
            var task = new WorkItem { ProjectId = projectId, Title = "T", AssigneeId = assigneeId };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();

            var user = await db.Users.FindAsync(assigneeId);
            db.Users.Remove(user!);
            await db.SaveChangesAsync();

            var reloaded = await db.WorkItems.FindAsync(task.Id);
            return new { TaskSurvived = reloaded != null, AssigneeIdAfter = reloaded?.AssigneeId };
        });

        await Exec("MOD04-C004", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d4@t.local");
            var assigneeId = await SeedTeamMember(t, "d4b@t.local");
            using var db = t.NewContext();
            var task = new WorkItem { ProjectId = projectId, Title = "T" };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();
            var item = new ChecklistItem { WorkItemId = task.Id, Label = "L", AssigneeId = assigneeId };
            db.ChecklistItems.Add(item);
            await db.SaveChangesAsync();

            var user = await db.Users.FindAsync(assigneeId);
            db.Users.Remove(user!);
            await db.SaveChangesAsync();

            var reloaded = await db.ChecklistItems.FindAsync(item.Id);
            return new { ItemSurvived = reloaded != null, AssigneeIdAfter = reloaded?.AssigneeId };
        });

        await Exec("MOD04-C005", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d5@t.local");
            var participantId = await SeedTeamMember(t, "d5b@t.local");
            using var db = t.NewContext();
            var meeting = new Meeting { ProjectId = projectId, Title = "M", ParticipantId = participantId };
            db.Meetings.Add(meeting);
            await db.SaveChangesAsync();

            var user = await db.Users.FindAsync(participantId);
            db.Users.Remove(user!);
            await db.SaveChangesAsync();

            var reloaded = await db.Meetings.FindAsync(meeting.Id);
            return new { MeetingSurvived = reloaded != null, ParticipantIdAfter = reloaded?.ParticipantId };
        });

        await Exec("MOD04-C006", async () =>
        {
            using var t = new TestDb();
            using var db = t.NewContext();
            var owner = new User { FullName = "Owner", Email = "d6@t.local", Role = UserRole.Manager };
            var member = new User { FullName = "Member", Email = "d6b@t.local", Role = UserRole.TeamMember };
            db.Users.AddRange(owner, member);
            await db.SaveChangesAsync();
            var project = new Project { Name = "P", OwnerId = owner.Id };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            db.Users.Remove(owner);
            await db.SaveChangesAsync();
            return "unreachable"; // expect throw before this
        });

        await Exec("MOD04-C007", async () =>
        {
            using var t = new TestDb();
            using var db = t.NewContext();
            var owner = new User { FullName = "Owner", Email = "d7@t.local", Role = UserRole.Manager };
            var member = new User { FullName = "Member", Email = "d7b@t.local", Role = UserRole.TeamMember };
            var author = new User { FullName = "Author", Email = "d7c@t.local", Role = UserRole.TeamMember };
            db.Users.AddRange(owner, member, author);
            await db.SaveChangesAsync();
            var project = new Project { Name = "P", OwnerId = owner.Id };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            var task = new WorkItem { ProjectId = project.Id, Title = "T" };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();
            var change = new StatusChange { WorkItemId = task.Id, FromStatus = WorkItemStatus.NotStarted, ToStatus = WorkItemStatus.InProgress, ChangedById = author.Id };
            db.StatusChanges.Add(change);
            await db.SaveChangesAsync();

            db.Users.Remove(author);
            await db.SaveChangesAsync();
            return "unreachable";
        });

        await Exec("MOD04-C008", async () =>
        {
            using var t = new TestDb();
            using var db = t.NewContext();
            var owner = new User { FullName = "Owner", Email = "d8@t.local", Role = UserRole.Manager };
            var member = new User { FullName = "Member", Email = "d8b@t.local", Role = UserRole.TeamMember };
            var author = new User { FullName = "Author", Email = "d8c@t.local", Role = UserRole.TeamMember };
            db.Users.AddRange(owner, member, author);
            await db.SaveChangesAsync();
            var project = new Project { Name = "P", OwnerId = owner.Id };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            var task = new WorkItem { ProjectId = project.Id, Title = "T" };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();
            var note = new ProgressNote { WorkItemId = task.Id, Text = "n", AuthorId = author.Id };
            db.ProgressNotes.Add(note);
            await db.SaveChangesAsync();

            db.Users.Remove(author);
            await db.SaveChangesAsync();
            return "unreachable";
        });

        await Exec("MOD04-C009", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d9@t.local");
            using var db = t.NewContext();
            var task = new WorkItem { ProjectId = projectId, Title = "T" };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();
            var parent = new ChecklistItem { WorkItemId = task.Id, Label = "Parent" };
            db.ChecklistItems.Add(parent);
            await db.SaveChangesAsync();
            var child = new ChecklistItem { WorkItemId = task.Id, Label = "Child", ParentId = parent.Id };
            db.ChecklistItems.Add(child);
            await db.SaveChangesAsync();

            db.ChecklistItems.Remove(parent);
            await db.SaveChangesAsync();

            var parentAfter = await db.ChecklistItems.FindAsync(parent.Id);
            var childAfter = await db.ChecklistItems.FindAsync(child.Id);
            return new { ParentSurvived = parentAfter != null, ChildSurvived = childAfter != null, ChildParentIdAfter = childAfter?.ParentId };
        });

        await Exec("MOD04-C010", async () =>
        {
            using var t = new TestDb();
            using var db = t.NewContext();
            var manager = new User { FullName = "Manager", Email = "d10@t.local", Role = UserRole.Manager };
            var member = new User { FullName = "Member", Email = "d10b@t.local", Role = UserRole.TeamMember };
            var ownerOnly = new User { FullName = "OwnerOnly", Email = "d10c@t.local", Role = UserRole.TeamMember };
            db.Users.AddRange(manager, member, ownerOnly);
            await db.SaveChangesAsync();
            var project = new Project { Name = "P", OwnerId = manager.Id };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            var task = new WorkItem { ProjectId = project.Id, Title = "T" };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();
            db.TaskOwners.Add(new TaskOwner { WorkItemId = task.Id, UserId = ownerOnly.Id });
            await db.SaveChangesAsync();

            db.Users.Remove(ownerOnly);
            await db.SaveChangesAsync();

            var taskAfter = await db.WorkItems.FindAsync(task.Id);
            var ownerRowGone = !await db.TaskOwners.AnyAsync(x => x.UserId == ownerOnly.Id);
            var usersCountAfter = await db.Users.CountAsync();
            return new { TaskSurvived = taskAfter != null, OwnerRowGone = ownerRowGone, UsersCountAfter = usersCountAfter };
        });

        await Exec("MOD04-C011", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d11@t.local");
            var memberId = await SeedTeamMember(t, "d11b@t.local");
            using var db = t.NewContext();
            var task = new WorkItem { ProjectId = projectId, Title = "T" };
            db.WorkItems.Add(task);
            await db.SaveChangesAsync();
            db.TaskOwners.Add(new TaskOwner { WorkItemId = task.Id, UserId = memberId });
            await db.SaveChangesAsync();

            db.WorkItems.Remove(task);
            await db.SaveChangesAsync();

            var taskOwnerRowsAfter = await db.TaskOwners.CountAsync();
            var workItemsAfter = await db.WorkItems.CountAsync();
            return new { TaskOwnerRowsAfter = taskOwnerRowsAfter, WorkItemsAfter = workItemsAfter };
        });

        await Exec("MOD04-C012", async () =>
        {
            using var t = new TestDb();
            var (ownerId, projectId) = await SeedManagerAndProject(t, "d12@t.local");
            int taskId, parentId, childId;
            using (var dbA = t.NewContext())
            {
                var task = new WorkItem { ProjectId = projectId, Title = "T" };
                dbA.WorkItems.Add(task);
                await dbA.SaveChangesAsync();
                taskId = task.Id;
                var parent = new ChecklistItem { WorkItemId = taskId, Label = "Parent" };
                dbA.ChecklistItems.Add(parent);
                await dbA.SaveChangesAsync();
                parentId = parent.Id;
                var child = new ChecklistItem { WorkItemId = taskId, Label = "Child", ParentId = parentId };
                dbA.ChecklistItems.Add(child);
                await dbA.SaveChangesAsync();
                childId = child.Id;
            }

            // Fresh context (same connection) that has never loaded the child row.
            using var dbB = t.NewContext();
            var parentInB = await dbB.ChecklistItems.FindAsync(parentId);
            dbB.ChecklistItems.Remove(parentInB!);
            await dbB.SaveChangesAsync();
            return "unreachable"; // expect DbUpdateException
        });

        // ================= MOD05: DbSeeder -- no modern equivalent exists at all =================
        foreach (var i in Enumerable.Range(1, 14))
            NotImplemented($"MOD05-C{i:000}");

        // ================= Write results =================
        var outDict = Results.OrderBy(r => r.CaseId, StringComparer.Ordinal)
            .ToDictionary(r => r.CaseId, r => (object)r);
        var json = JsonSerializer.Serialize(outDict, new JsonSerializerOptions { WriteIndented = true });
        var outPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "modern-results.json");
        await File.WriteAllTextAsync(outPath, json);
        Console.WriteLine($"Wrote {Results.Count} case results to {Path.GetFullPath(outPath)}");
    }

    private static async Task<int> SeedManager(TestDb t, string email)
    {
        using var db = t.NewContext();
        var u = new User { FullName = "Manager", Email = email, Role = UserRole.Manager };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> SeedTeamMember(TestDb t, string email)
    {
        using var db = t.NewContext();
        var u = new User { FullName = "Member", Email = email, Role = UserRole.TeamMember };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<(int OwnerId, int ProjectId)> SeedManagerAndProject(TestDb t, string ownerEmail)
    {
        using var db = t.NewContext();
        var owner = new User { FullName = "Manager", Email = ownerEmail, Role = UserRole.Manager };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var project = new Project { Name = "Proj", OwnerId = owner.Id };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return (owner.Id, project.Id);
    }
}
