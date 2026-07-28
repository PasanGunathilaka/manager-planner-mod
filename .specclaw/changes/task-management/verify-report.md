# Verification Report: task-management

**Verified:** 2026-07-28
**Model:** Claude Sonnet 5
**Verdict:** PASS

## Acceptance Criteria

- ✅ **AC1:** `dotnet build` at the solution root succeeds with 0 errors — Build Output: `"Build succeeded.\n    0 Warning(s)\n    0 Error(s)"`.

- ✅ **AC2:** Submitting the full form persists a `WorkItem` with correct `ProjectId`, trimmed `Title`, trimmed `Description`, chosen `ObjectiveId`/`AssigneeId`/`Deadline`, `IsDiscovered == true`, `DiscoveredInMeetingId == null`, and the grid updates in place without a full page reload — `ProjectDetail.razor`'s `AddTaskAsync()` computes `var description = string.IsNullOrWhiteSpace(_newTaskDescription) ? null : _newTaskDescription.Trim();` before calling `PlanningService.AddTaskAsync(Id, _newTaskTitle, description, _newTaskAssigneeId, _newTaskDeadline, _newTaskIsDiscovered, _newTaskObjectiveId)`, exactly mirroring the legacy caller pre-processing FR1 requires. `PlanningService.AddTaskAsync` trims Title (`Title = title.Trim()`), sets `ObjectiveId = objectiveId`, `AssigneeId = assigneeId`, `Deadline = deadline`, `IsDiscovered = isDiscovered`, and never touches `DiscoveredInMeetingId` — `WorkItem.cs` declares `public int? DiscoveredInMeetingId { get; set; }` with no initializer, so it stays `null`. After the awaited call, the page reassigns `_objectives`/`_ungroupedTasks` in-component (no `NavigationManager.NavigateTo`, no meta-refresh) — a same-circuit Blazor Server re-render, not a page reload.
  - Also confirmed at runtime during build: a scratch console app querying the live SQLite database directly showed a fully-filled submission ("Full form task", Objective A, deadline 2026-08-15, description with leading/trailing whitespace, Discovered checked) persisted with `Description='Some description text'` (21 chars, whitespace trimmed), `ObjectiveId=10`, `Deadline=8/15/2026`, `IsDiscovered=True`, `DiscoveredInMeetingId=<null>`.

- ✅ **AC3:** Title-only submission persists `ObjectiveId == null`, `AssigneeId == null`, `Deadline == null`, `Description == null`, `IsDiscovered == false`, and appears in "Ungrouped" — the `@code` block's field declarations default every other field: `private int? _newTaskObjectiveId;`, `private int? _newTaskAssigneeId;`, `private DateTime? _newTaskDeadline;`, `private string? _newTaskDescription;`, `private bool _newTaskIsDiscovered;` (all unset ⇒ `null`/`false`). With `_newTaskDescription == null`, `string.IsNullOrWhiteSpace(null)` is `true` so `description` passed to the service is `null`. `GetUngroupedTasksForProjectAsync` queries `db.WorkItems.Where(t => t.ProjectId == projectId && t.ObjectiveId == null)`, and the page's `@if (_ungroupedTasks is { Count: > 0 }) { <h3>Ungrouped</h3> ... }` will render the row.
  - Confirmed at runtime: submitting only a title persisted a task with `Description=<null>`, `ObjectiveId=<null>`, `AssigneeId=<null>`, `Deadline=<null>`, `IsDiscovered=False`, rendered under the "Ungrouped" heading.

- ✅ **AC4:** Empty/whitespace-only or >120-char titles show the validation message inline and create no row; other field values aren't discarded — `PlanningRules.ValidateTaskTitle`: `var t = title?.Trim() ?? string.Empty; if (t.Length == 0) throw new ValidationException("Task title is required."); if (t.Length > MaxTaskTitle) throw new ValidationException($"Task title cannot exceed {MaxTaskTitle} characters.");` (with `MaxTaskTitle = 120`) runs *before* `db.WorkItems.Add(task)`/`SaveChangesAsync()`, so no row is created on failure. `ProjectDetail.razor`'s `AddTaskAsync()` wraps the service call in `try { ... } catch (ManagerPlanner.Core.Validation.ValidationException ex) { _taskErrorMessage = ex.Message; }`, rendered via `@if (!string.IsNullOrEmpty(_taskErrorMessage)) { <p style="color: red;">@_taskErrorMessage</p> }`. Because the field-reset lines sit *after* the throwing call inside the same `try`, an exception skips them — other entered field values remain bound and visible.
  - Confirmed at runtime: both cases showed "Task title is required." and "Task title cannot exceed 120 characters." inline, no new row created either time.
  - ⚠️ Edge case: no client-side `maxlength` is set on the Title `InputText`, so the >120-char case relies entirely on this server-side (in-process) check firing before submit completes — confirmed above, but worth noting there is no earlier UX guard.

- ✅ **AC5:** A task submitted with an Objective selected appears in that objective's section and its "No tasks yet." placeholder no longer renders — `GetPlannerForProjectAsync` eager-loads `.Include(o => o.Tasks)...`, and the razor loop does `@if (objective.Tasks.Count == 0) { <p>No tasks yet.</p> } else { <table>...<TaskRow WorkItem="task" />...</table> }`, switching away from the placeholder the moment `Tasks.Count > 0`. Confirmed at runtime.

- ✅ **AC6:** The assignee `<select>` is populated from `GetTeamMembersAsync` and renders only "— Unassigned —" with zero team members, without erroring — `GetTeamMembersAsync`: `db.Users.Where(u => u.Role == UserRole.TeamMember && u.IsActive).OrderBy(u => u.FullName)`, matching FR2 exactly. The razor markup: `<option value="">&mdash; Unassigned &mdash;</option> @foreach (var member in _teamMembers ?? new List<User>()) { <option value="@member.Id">@member.FullName</option> }` — an empty list simply produces zero iterations, no null-reference risk. Confirmed at runtime (zero team members currently seeded).

- ✅ **AC7:** "Ungrouped" renders only when the project has ≥1 ungrouped task, with no empty heading otherwise — `@if (_ungroupedTasks is { Count: > 0 }) { <h3>Ungrouped</h3> <table>...</table> }` — the pattern match requires both non-null and `Count > 0`, so a zero-count (or not-yet-loaded) list renders nothing, not an empty heading. Confirmed at runtime (no "Ungrouped" heading before any ungrouped task existed).

- ✅ **AC8:** Each row shows Title, deadline-or-nothing, assignee-or-"Unassigned", status text, with no OVERDUE/discovered badge, no checklist markup, no status-change control — `TaskRow.razor`: `@WorkItem.Title`, then `@if (WorkItem.Deadline.HasValue) { <div>@WorkItem.Deadline.Value.ToString("yyyy-MM-dd")</div> }`, then `@(WorkItem.Assignee?.FullName ?? "Unassigned")` plus `@StatusText` where `StatusText` maps `NotStarted => "Not started"`, `InProgress => "In progress"`, `Blocked => "Blocked"`, `Done => "Done"`. The third `<td>` is a bare `&mdash;` — no checklist tree, no buttons, no badge text anywhere in the component.
  - ⚠️ Edge case: since no status-change path exists anywhere in this diff, every persisted task is `WorkItemStatus.NotStarted` (the entity default), so "Not started" is the only status text ever observed today — consistent with the spec's own note, not a gap.

- ✅ **AC9:** No `PlanningService` method exists beyond the specified nine — reading `PlanningService.cs` in full shows exactly: `GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`, `GetCurrentManagerIdAsync`, `AddObjectiveAsync`, `GetPlannerForProjectAsync`, `AddTaskAsync`, `GetTeamMembersAsync`, `GetUngroupedTasksForProjectAsync` — nine methods, matching the six-existing-plus-three-added list verbatim, with no extras (e.g., no `discoveredInMeetingId` parameter on `AddTaskAsync`'s signature).

## Test Results

No tests configured — `test_command`/`lint_command` are unset in `config.yaml`; no automated test run accompanies this change. `dotnet build` is the only automated gate, and it passes with 0 errors/0 warnings.

## Issues Found

1. **`GetUngroupedTasksForProjectAsync` initially omitted `.AsSplitQuery()`** — it has the same multi-collection `Include`/`ThenInclude` shape (`Owners.ThenInclude(User)` + `Checklist`) that `GetPlannerForProjectAsync` needed `.AsSplitQuery()` for per FR4, and would have logged the same EF Core cartesian-product warning via the ungrouped-tasks path instead. **Fixed during verify**: added `.AsSplitQuery()` to `GetUngroupedTasksForProjectAsync` for consistency; `dotnet build` reconfirmed 0 errors after the change. Not tied to a failing AC — was informational only, now resolved.

## Summary

**Passed:** 9/9 criteria
**Failed:** 0/9 criteria
**Verdict:** PASS
