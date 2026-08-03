# Tasks: Accountability reporting (promised-vs-delivered verdicts)

**Change:** accountability-reporting
**Created:** 2026-08-03
**Total Tasks:** 3

## Summary

Three tasks across two waves: (1) port `AccountabilityRow` and the two
`GetAccountabilityReportAsync`/`GetAccountabilityForAllProjectsAsync`
methods exactly, preserving the `IsOverdue`-before-promise quirk; (2a) add
the project-scoped Accountability section to `ProjectDetail.razor` plus
`TaskRow.razor`'s new `NoteAdded` callback; (2b) add the new
all-projects `/accountability` page plus its nav link. T2 and T3 touch
completely disjoint files (`ProjectDetail.razor`/`TaskRow.razor` vs. a new
`Accountability.razor`/`MainLayout.razor`) — per `.specclaw/context.md`'s
established pattern, spawn their coding agents in parallel rather than
sequentially. No task adds a verdict-acting UI, a project filter or
refresh button on `/accountability`, or a distinct "Owner" label — those
stay out of scope per spec.md NFR2/AC17.

## Tasks

### Wave 1 — Core computation

- [x] `T1` — Add `AccountabilityRow` to `Reports.cs`; add `GetAccountabilityReportAsync`/`GetAccountabilityForAllProjectsAsync` to `PlanningService`
  - Files: `src/ManagerPlanner.Core/Services/Reports.cs`, `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: medium
  - Depends: none
  - Notes: Ground-truth against the real legacy source at `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\Reports.cs` (full file, 67 lines) and `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs:269-346`, not the doc summary. In `Reports.cs`, add `public class AccountabilityRow` alongside the existing `ProjectSummary`: settable `int WorkItemId`, `string TaskTitle = string.Empty`, `string ProjectName = string.Empty`, `string AssigneeName = "(unassigned)"`, `WorkItemStatus Status`, `DateTime? Deadline`, `DateTime? LatestPromisedDate`, `string? LatestPromiseText`, `DateTime? LatestPromiseRecordedUtc`, `DateTime? CompletedUtc`, `bool IsOverdue`, `bool PromiseBroken`, `bool PromiseKept`; plus a read-only `string Verdict => PromiseKept ? "Kept promise" : PromiseBroken ? "BROKE promise" : IsOverdue ? "Overdue (no promise)" : LatestPromisedDate.HasValue ? "Promise pending" : "On track";` — **this exact `if`/precedence order is load-bearing** (spec.md FR4): `IsOverdue` is checked before `LatestPromisedDate.HasValue` deliberately, preserving the legacy's "Overdue (no promise)" mislabeling of a task that has an active, not-yet-due promise. Do not reorder these checks. In `PlanningService.cs`, add `GetAccountabilityReportAsync(int projectId)`: open the factory context, `var now = DateTime.UtcNow;`, load `db.WorkItems.Include(t => t.Assignee).Include(t => t.Project).Include(t => t.Notes).Where(t => t.ProjectId == projectId).ToListAsync()`, then for each task: `var latestPromise = t.Notes.Where(n => n.IsPromise && n.PromisedDate.HasValue).OrderByDescending(n => n.CreatedUtc).FirstOrDefault();`, build the row (`WorkItemId = t.Id, TaskTitle = t.Title, ProjectName = t.Project?.Name ?? string.Empty, AssigneeName = t.Assignee?.FullName ?? "(unassigned)", Status = t.Status, Deadline = t.Deadline, CompletedUtc = t.CompletedUtc, LatestPromisedDate = latestPromise?.PromisedDate, LatestPromiseText = latestPromise?.Text, LatestPromiseRecordedUtc = latestPromise?.CreatedUtc`), then `row.IsOverdue = t.Deadline.HasValue && t.Deadline.Value < now && t.Status != WorkItemStatus.Done;`, then `if (latestPromise?.PromisedDate is DateTime promised) { if (t.Status == WorkItemStatus.Done) { row.PromiseKept = t.CompletedUtc.HasValue && t.CompletedUtc.Value.Date <= promised.Date; row.PromiseBroken = !row.PromiseKept; } else { row.PromiseBroken = promised.Date < now.Date; } }` — note the strict `<` (not `<=`) for the not-Done branch (spec.md AC9). Return `rows.OrderByDescending(r => r.PromiseBroken).ThenByDescending(r => r.IsOverdue).ThenBy(r => r.Deadline ?? DateTime.MaxValue).ToList();` — do not add any further tie-break key (spec.md AC12/Edge Cases). Add `GetAccountabilityForAllProjectsAsync()`: `var projectIds = await db.Projects.OrderBy(p => p.Name).Select(p => p.Id).ToListAsync();` then for each id call the method above (a fresh context per call is fine — matches the legacy shape of one call per project) and concatenate, then re-sort with the **extra** `ProjectName` key: `.OrderByDescending(r => r.PromiseBroken).ThenByDescending(r => r.IsOverdue).ThenBy(r => r.ProjectName).ThenBy(r => r.Deadline ?? DateTime.MaxValue).ToList();` (spec.md FR3/AC13). Both methods use `IDbContextFactory<PlanningDbContext>` like all fifteen existing methods.

    Verify manually: `dotnet build` (AC1). For the pure `Verdict` branches (AC2–AC6), write a small scratch console app (or equivalent quick check) constructing `AccountabilityRow` instances matching `.specclaw/baseline/fixtures/GM-008.json` through `GM-012.json`'s exact `input` fields and confirm `.Verdict` equals each fixture's exact `output.verdict` string. For the stateful boundaries (AC7–AC13), seed real `Project`/`WorkItem`/`ProgressNote`/`StatusChange` rows against the live SQLite DB (via a scratch console app calling `PlanningService` in-process, mirroring `GM-013` through `GM-018`'s scenarios but computing promise/deadline/completion dates as offsets from the actual current run-time `now` rather than the fixtures' anchored `2026-07-30` literals — e.g. "promised date = today" for AC9, "promised date = yesterday" for AC10) and confirm `GetAccountabilityReportAsync`/`GetAccountabilityForAllProjectsAsync` produce the exact `PromiseKept`/`PromiseBroken`/`Verdict`/order shape each fixture describes.

### Wave 2 — UI (T2 and T3 are file-disjoint — run their coding agents in parallel)

- [x] `T2` — Accountability section on `ProjectDetail.razor`; `TaskRow.razor` gains a `NoteAdded` callback
  - Files: `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`, `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: In `TaskRow.razor`, add `[Parameter] public EventCallback NoteAdded { get; set; }` alongside the existing `StatusChanged`/`Meetings` parameters, and in `AddNoteAsync`'s success path (after the existing `_notes = await PlanningService.GetNotesForTaskAsync(WorkItem.Id);` reload), add `await NoteAdded.InvokeAsync();` — no other change to `TaskRow.razor`. In `ProjectDetail.razor`: add `Meetings="_meetings"`'s sibling parameter, `NoteAdded="RefreshAsync"`, to both existing `<TaskRow WorkItem="task" StatusChanged="RefreshAsync" Meetings="_meetings" />` usages (per-objective loop and Ungrouped section). Add a private `List<AccountabilityRow>? _accountabilityRows` field, loaded in both `OnInitializedAsync` and `RefreshAsync` via `_accountabilityRows = await PlanningService.GetAccountabilityReportAsync(Id);` (alongside the existing `_summary`/`_objectives`/`_teamMembers`/`_ungroupedTasks`/`_meetings` loads — same method, no new load path). Add a new "Accountability" section (after the existing Meetings section): a read-only `MudSimpleTable` with columns Task / Assignee / Status / Deadline / Promised / Verdict, rendering `_accountabilityRows` in the order the service already returns them (no client re-sort); show `"No tasks yet."` when empty, matching the existing "No objectives yet."/"No meetings yet." convention. Add a small private helper, e.g. `private Color VerdictColor(string verdict) => verdict switch { "Kept promise" => Color.Success, "BROKE promise" => Color.Error, "Overdue (no promise)" => Color.Warning, "Promise pending" => Color.Info, _ => Color.Default };`, and render each row's Verdict cell as `<MudChip T="string" Color="@VerdictColor(row.Verdict)" Size="Size.Small">@row.Verdict</MudChip>` (matching the existing status-badge `MudChip` pattern already used elsewhere on this page). Deadline/Promised cells render as `yyyy-MM-dd` (UTC, no local-time formatting) or `"—"` when null.

    Verify manually: `dotnet build` (AC1, shared with T3). Through the running app (per `.specclaw/context.md`'s established fallback — `form_input`, `read_page` immediately before every click, default straight to JS-dispatched `element.click()`): confirm the Accountability section renders one row per task with the correct Verdict text and `Color` (AC14) — construct a few tasks/notes covering at least one "BROKE promise"/"Kept promise"/"On track" case each, cross-checked by direct DB inspection of `WorkItem`/`ProgressNote`; add a new note via a `TaskRow` and confirm the Accountability section's rows reload with the updated verdict without a manual page refresh (AC16); confirm no button/link anywhere in this section lets a user edit/act on a row (AC17, this surface's half).

- [x] `T3` — New `/accountability` page (all-projects) and its nav link
  - Files: `src/ManagerPlanner.Web/Components/Pages/Accountability.razor`, `src/ManagerPlanner.Web/Components/Layout/MainLayout.razor`
  - Estimate: small
  - Depends: `T1`
  - Notes: Create `Accountability.razor` following `Projects.razor`'s exact page-file shape (`@page "/accountability"`, `@inject PlanningService PlanningService`, `<PageTitle>Accountability</PageTitle>`, an `<h1>`): load `private List<AccountabilityRow>? _rows;` in `OnInitializedAsync` via `_rows = await PlanningService.GetAccountabilityForAllProjectsAsync();` — no refresh button, no project-select filter (spec.md FR6/AC17). Render a read-only `MudSimpleTable` with columns Project / Task / Assignee / Status / Deadline / Promised / Verdict, in the exact order the service returns (no client re-sort), reusing the identical `VerdictColor`/`MudChip` rendering shape T2 adds to `ProjectDetail.razor` (a small local copy of the same switch expression is fine — no shared component extraction needed for one extra use site, matching this project's "extract only when two call sites in the *same* change need it" pattern). Show `"No tasks yet."` when `_rows` is empty. In `MainLayout.razor`, add `<MudNavLink Href="/accountability" Icon="@Icons.Material.Filled.Assessment">Accountability</MudNavLink>` inside the existing `MudNavMenu`, after the `/projects` link.

    Verify manually: `dotnet build` (AC1, shared with T2). Through the running app: navigate via the new nav link and confirm the page renders rows spanning more than one project, each with the correct `Project` column value (AC15); with two projects whose rows would otherwise tie on `PromiseBroken`/`IsOverdue`/`Deadline`, confirm they're ordered by `ProjectName` ascending (AC13, cross-checked against T1's own verification of the same rule at the service level); confirm no filter control or refresh button exists on this page (AC17, this surface's half).

---

## Legend

- `[ ]` Pending
- `[~]` In Progress
- `[x]` Complete
- `[!]` Failed

**Task format:**
```
- [ ] `T<n>` — <title>
  - Files: <files to create/modify>
  - Estimate: small | medium | large
  - Depends: <task ids> (if any)
  - Notes: <additional context>
```
