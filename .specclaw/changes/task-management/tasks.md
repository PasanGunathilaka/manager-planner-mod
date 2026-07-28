# Tasks: Task (WorkItem) creation and viewing

**Change:** task-management
**Created:** 2026-07-28
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) add the three `PlanningService` methods
this item needs plus the `.AsSplitQuery()` follow-up, (2) extend
`/projects/{id}` with the unified add-task form, a `TaskRow` component,
real per-objective rows, and the new "Ungrouped" section, then verify
end-to-end. No task touches status-change controls, checklist rendering,
badges, `TaskOwner` assignment, or task selection/deletion — those stay
out of scope per spec.md NFR2/AC9.

## Tasks

### Wave 1 — Core business logic

- [x] `T1` — Add `AddTaskAsync`, `GetTeamMembersAsync`,
  `GetUngroupedTasksForProjectAsync` to `PlanningService`; add
  `.AsSplitQuery()` to `GetPlannerForProjectAsync`
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at
    `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs`
    (lines 95-114 for `AddTaskAsync`, ~lines 32-38 for `GetTeamMembersAsync`
    — search the file directly, don't guess line numbers), not the doc
    summary. `AddTaskAsync(int projectId, string title, string?
    description, int? assigneeId, DateTime? deadline, bool isDiscovered =
    false, int? objectiveId = null)` — **no `discoveredInMeetingId`
    parameter** (design.md Key Decision 1): validate via
    `PlanningRules.ValidateTaskTitle(title)`, construct a `WorkItem` with
    `Title = title.Trim()` but **`Description = description` verbatim, no
    `.Trim()`** (design.md Key Decision 2 — this is a real fidelity
    nuance in the legacy code, confirm by reading it directly), `Status`/
    `CreatedUtc` left to the entity's own defaults, save, return the
    `WorkItem`. `GetTeamMembersAsync()`: `_db.Users.Where(u => u.Role ==
    UserRole.TeamMember && u.IsActive).OrderBy(u =>
    u.FullName).ToListAsync()`, ported exactly. `GetUngroupedTasksForProjectAsync(int
    projectId)` — new, no legacy equivalent (design.md Key Decision 3):
    `db.WorkItems.Where(t => t.ProjectId == projectId && t.ObjectiveId ==
    null).Include(t => t.Assignee).Include(t =>
    t.Owners).ThenInclude(o => o.User).Include(t =>
    t.Checklist).ToListAsync()` — same eager-load shape as
    `GetPlannerForProjectAsync`'s task Includes. Add `.AsSplitQuery()` to
    `GetPlannerForProjectAsync`'s existing query chain (design.md Key
    Decision 7 — acts on the follow-up `.specclaw/context.md` already
    flagged). All three new methods use
    `IDbContextFactory<PlanningDbContext>` like the six existing methods —
    open/dispose a short-lived context per call.

### Wave 2 — Page + verification

- [x] `T2` — Unified add-task form, `TaskRow` component, real grid rows,
  Ungrouped section, end-to-end verification
  - Files: `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` (new),
    `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` (modify)
  - Estimate: medium-large
  - Depends: `T1`
  - Notes:
    **`TaskRow.razor`** — a small component, `[Parameter] public WorkItem
    Task { get; set; } = null!;`, rendering the 3-column row: task cell
    (`Task.Title`, plus `Task.Deadline?.ToString("yyyy-MM-dd")` or nothing
    if null — UTC, not local time, per design.md Key Decision 5); owner/
    status cell (`Task.Assignee?.FullName ?? "Unassigned"`, plus status
    text via the same mapping as legacy `TaskRowVm.Humanize`
    (`RowViewModels.cs:87-94`): `NotStarted` → "Not started", `InProgress`
    → "In progress", `Blocked` → "Blocked", `Done` → "Done"); checklist
    cell left empty (a placeholder like `—` is fine — item 5 renders real
    content here). No OVERDUE/discovered badges, no status buttons —
    don't add them (spec.md NFR2).

    **`ProjectDetail.razor`** — add one "Add task" form (not per-objective)
    near the top of the Planner Grid section, modeled on the existing
    add-objective form's pattern (`EditForm` + inline `ValidationException`
    message + in-place refresh, no full reload): Title (`InputText`,
    required), Objective (`<select>` bound to a nullable int field, first
    option `<option value="">— Ungrouped —</option>` then one `<option>`
    per loaded objective); Assignee (`<select>`, first option `<option
    value="">— Unassigned —</option>` then one per `GetTeamMembersAsync()`
    result, fetched once in `OnInitializedAsync`/`RefreshAsync` alongside
    the existing summary/objectives fetches); Deadline
    (`<InputDate @bind-Value="_newTaskDeadline" />` where the field is
    `DateTime?` — if the browser gives a bare date, treat it as already
    UTC, don't attempt a timezone conversion, matching design.md Key
    Decision 5); Description (`<InputTextArea>`, optional); "Discovered in
    a meeting" `<InputCheckbox>`. On submit, call `AddTaskAsync` with the
    Objective/Assignee `<select>` values parsed to `int?` (empty string →
    `null`), catch `ValidationException` and show `.Message` inline
    exactly like the existing add-objective form, then on success clear
    the form fields and re-fetch `GetPlannerForProjectAsync` +
    `GetUngroupedTasksForProjectAsync` to refresh in place.

    Replace each objective's `<p>No tasks yet.</p>` with `@foreach (var t
    in objective.Tasks) { <TaskRow Task="t" /> }` when `objective.Tasks`
    is non-empty, keeping the "No tasks yet." text only when it's empty.
    Add a new "Ungrouped" section below the per-objective loop: a heading
    (e.g. `<h3>Ungrouped</h3>`) plus one `<TaskRow>` per item from
    `GetUngroupedTasksForProjectAsync`, **rendered only when that list is
    non-empty** (spec.md AC7 — no empty "Ungrouped" heading on projects
    where every task has an objective).

    Verify manually: `dotnet build` (AC1); through the running app, submit
    the full form (all fields filled, checkbox checked) and confirm the
    persisted `WorkItem`'s fields via direct DB inspection match exactly,
    including that `Description` preserves any leading/trailing
    whitespace typed (AC2); submit with only a title and confirm the
    resulting task has all other fields `null`/`false` and renders under
    "Ungrouped" (AC3); submit an empty and an over-120-char title and
    confirm the inline validation message with no row created (AC4);
    confirm a task submitted with an Objective selected appears in that
    objective's section, no longer showing "No tasks yet." for it (AC5);
    confirm the assignee `<select>` currently renders with only "—
    Unassigned —" (no seeded team members exist yet) and does not error
    (AC6); confirm "Ungrouped" does not render at all on a project with
    zero ungrouped tasks (AC7); confirm every rendered row shows title,
    deadline-or-nothing, assignee-or-"Unassigned", and "Not started"
    status text, with no badge/checklist/status-button markup anywhere
    (AC8). Use `form_input` (not `computer.type`) for every field and
    re-`read_page` immediately before every click if testing via
    claude-in-chrome — see `.specclaw/context.md`'s Key Patterns for why,
    including the documented fallback (a scratch console app calling
    `PlanningService` in-process) if browser click-dispatch wedges again
    as it did during `planner-grid`'s verification.

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
