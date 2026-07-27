# Spec: Objective grouping and the planner grid

**Change:** planner-grid
**Created:** 2026-07-27
**Status:** 🟡 Draft

## Overview

Adds `Objective` creation/grouping to the rebuild: two new `PlanningService`
methods ported from the real legacy source, and a new "Planner Grid"
section on the existing `/projects/{id}` page. The grid's static shell
(add-objective form, 3-column header, per-objective heading) is built now;
task-row content (owners, status, checklist, inline add-task) is explicitly
deferred to items 3–5, which will populate the same shell.

## Requirements

### Functional Requirements

1. **FR1 — `AddObjectiveAsync(projectId, title, keyResult = null)`.**
   Validates `title` via the already-ported
   `PlanningRules.ValidateObjectiveTitle` (≤150 chars), trims it, sets
   `SortOrder` to the current count of objectives for that project
   (append-only — matches legacy's `var order = await
   _db.Objectives.Where(o => o.ProjectId == projectId).CountAsync();`
   exactly), saves, and returns the created `Objective`.
2. **FR2 — `GetPlannerForProjectAsync(projectId)`.** Returns the project's
   objectives ordered by `SortOrder`, with the exact legacy eager-load
   chain: `.Include(o => o.Tasks).ThenInclude(t => t.Assignee)`,
   `...ThenInclude(t => t.Owners).ThenInclude(w => w.User)`,
   `...ThenInclude(t => t.Checklist)`. `Objective.Tasks` will be empty for
   every objective until backlog item 3 (Task/WorkItem) exists — this is
   expected, not a bug.
3. **FR3 — Add-objective form on `/projects/{id}`.** A single required
   Title field (no Key Result input — see Notes) + "Add" button, calling
   `AddObjectiveAsync`. On a thrown `ValidationException`, its `.Message`
   is shown inline and no objective is created (same pattern as
   `Projects.razor`'s create-project form from `project-management`). On
   success, the objective list refreshes in place — no full page reload.
4. **FR4 — Fixed 3-column header.** "Tasks" | "Owner / status" | "Progress
   checklist", always rendered on `/projects/{id}` regardless of whether
   the project has any objectives yet (matches the legacy layout, where
   the header is outside the objectives loop).
5. **FR5 — Per-objective rendering.** Each objective renders as a heading
   (Title only) followed by a "No tasks yet." placeholder in place of
   task rows.
6. **FR6 — Empty state.** If a project has zero objectives, the section
   shows a "No objectives yet." message beneath the always-visible header
   (analogous to `Projects.razor`'s "No projects yet.").

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** Both new `PlanningService` methods use
   `IDbContextFactory<PlanningDbContext>` (already the constructor's
   type), consistent with the four existing methods — no direct
   `PlanningDbContext` injection.
2. **NFR2 — Scope discipline.** No task-row rendering (owners, status,
   checklist, OVERDUE/discovered badges), no inline "add task to this
   objective" form, and no objective edit/delete/reorder exist after this
   change — those are items 3, 4, 5 (task rows) and remain undecided/
   unbuilt (objective management) since no legacy UI exists for them either.
3. **NFR3 — No Key Result input.** The add-objective form has no field for
   `Objective.KeyResult` — confirmed absent from the real legacy
   `PlannerGridView.axaml`/`MainViewModel.AddObjectiveCommand`, which
   always calls `AddObjectiveAsync` with `keyResult` omitted (null).

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Submitting a valid title via the add-objective form persists
   a new `Objective` (correct `ProjectId`, trimmed `Title`, `KeyResult ==
   null`), and the section updates to show it without a full page reload.
3. **AC3** — Submitting an empty/whitespace-only title, or a title over
   150 characters, shows the `PlanningRules` validation message inline
   and creates no `Objective` row.
4. **AC4** — Adding a second objective to the same project results in
   `SortOrder == 1` for it (the first objective has `SortOrder == 0`) —
   confirmed by direct inspection of the persisted rows, proving the
   append-only, count-based assignment matches legacy.
5. **AC5** — The "Tasks" | "Owner / status" | "Progress checklist" header
   renders on `/projects/{id}` even for a project with zero objectives.
6. **AC6** — Each objective renders its Title and a "No tasks yet."
   placeholder — no owner/status/checklist content, no inline add-task
   control, anywhere in the rendered output.
7. **AC7** — No `PlanningService` method beyond the four existing ones
   (`GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`,
   `GetCurrentManagerIdAsync`) plus the two added here
   (`AddObjectiveAsync`, `GetPlannerForProjectAsync`) exists anywhere in
   the diff.
8. **AC8** — No Key Result input field exists in the rendered add-objective
   form.

## Edge Cases

- **Zero objectives.** Header still renders; "No objectives yet." shown
  beneath it (FR6).
- **Overlong or whitespace-only title.** Rejected per FR3/AC3; existing
  objectives/list unaffected.
- **Duplicate objective titles within the same project.** Allowed — no
  uniqueness rule exists in `PlanningRules.ValidateObjectiveTitle` or
  anywhere in the legacy domain model; do not invent one.
- **Concurrent adds racing the count-based `SortOrder`.** Not specially
  handled — this matches the legacy service's own behavior exactly (no
  transaction/locking there either); not a new risk introduced here, and
  not a defect to silently fix in this port.

## Dependencies

- **Depends on:** `scaffold-blazor-solution` (item 0, entities/rules) and
  `project-management` (item 1, `/projects/{id}` to extend).
- **Blocks:** item 3 (Task/WorkItem), which needs this grid shell to
  attach task-row rendering and the inline add-task form to; transitively
  blocks items 4 (status) and 5 (checklist), which populate the same rows.

## Notes

Two proposal open questions are resolved by proceeding to this spec: (1)
the Key Result input is omitted, matching confirmed legacy behavior; (2)
the grid's static shell is built now, ahead of item 3's task data, per
rebuild-backlog's own merge rationale for this item.
