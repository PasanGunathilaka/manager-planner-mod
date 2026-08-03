# Spec: Project deletion (cascade)

**Change:** project-deletion
**Created:** 2026-08-03
**Status:** 🟡 Draft

## Overview

Adds the ability to permanently delete a project, cascading to every
objective, task, meeting, and everything those in turn own — the
broadest cascade in the whole system, and the last data-model-driven
backlog item. Reuses the `IDialogService.ShowMessageBoxAsync`
confirmation pattern `task-deletion` already proved working in this
codebase. Like `task-deletion`, this is **not** a byte-for-byte port of
the legacy service method — the same `IDbContextFactory`-per-call
cascade gap `task-deletion` discovered (and flagged as certain to
recur here) was independently confirmed *before* planning began, not
left as a build-time surprise: see proposal.md's reproduction against the
full `GM-024` shape.

## Requirements

### Functional Requirements

1. **FR1 — `DeleteProjectAsync(projectId)`.** Same observable contract as
   `../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:64-70`
   (find, no-op if missing, remove, save), **but not a byte-for-byte
   port** — it must load `.Include(p => p.Tasks).ThenInclude(t =>
   t.Checklist)` before removing. Confirmed by direct reproduction
   (documented in proposal.md) that a plain `FindAsync` + `Remove`
   throws `SQLite Error 19: FOREIGN KEY constraint failed` whenever any
   task under the project has a nested (parent+child) checklist item —
   the exact same root cause `task-deletion` (BL-009) discovered for
   `DeleteTaskAsync`, one level deeper (`Project` → `WorkItem` →
   `ChecklistItem`). The `.Include` chain was also confirmed *sufficient*
   against the full `GM-024` shape — `Objective`/`ProgressNote`/
   `StatusChange`/`TaskOwner`/`Meeting` all cascade correctly from a cold
   context without needing to be included; only the self-referencing
   `ChecklistItem` tree requires it.
2. **FR2 — A "Delete" icon button on each project row in
   `Projects.razor`**, not a page-level "select a project, then click
   Delete" flow — every prior item in this rebuild (status, checklist,
   notes, task deletion) already acts directly on a row rather than
   reviving the legacy's selection-based interaction model, and this item
   follows the same shape. The button uses `@onclick:stopPropagation`
   so clicking it does not also trigger the row's existing `Href`
   navigation to `/projects/{id}`.
3. **FR3 — A confirmation dialog via MudBlazor's `IDialogService`**,
   reusing the exact mechanism `task-deletion` established
   (`ShowMessageBoxAsync`), reproducing the real legacy text verbatim,
   confirmed by reading
   `../manager-planner/src/ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:218-230`:
   `"Delete project '{name}' and all its objectives, tasks, checklist
   items and notes?\nThis cannot be undone."` Called with `yesText:
   "Delete"`, `cancelText: "Cancel"` — the same two-button shape
   `task-deletion` already uses.
4. **FR4 — On confirm, call `DeleteProjectAsync` then reload the project
   list.** Reuses the existing `AddProjectAsync` handler's own
   reload-after-mutate shape already on this page (`_projects = await
   PlanningService.GetProjectsAsync();`) — no new refresh mechanism
   needed, since `Projects.razor` has no parent/child component
   relationship the way `TaskRow`/`ProjectDetail` do.
5. **FR5 — Canceling the dialog makes no service call.**
   `DeleteProjectAsync` is only invoked when `ShowMessageBoxAsync`
   returns `true`.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** `DeleteProjectAsync` uses
   `IDbContextFactory<PlanningDbContext>` like all eighteen existing
   `PlanningService` methods.
2. **NFR2 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: any change to `ProjectDetail.razor` (the
   legacy delete capability is scoped to the Projects list only); a
   page-level "select a project" flow; any undo/soft-delete mechanism;
   any change to `DeleteTaskAsync`, `ChangeStatusAsync`, or any other
   existing `PlanningService` method.
3. **NFR3 — Reuse the existing reload-after-mutate shape**, not a new
   callback pattern — `Projects.razor` is a single page with no child
   component wiring `TaskRow`/`ProjectDetail`'s
   `EventCallback`-to-`RefreshAsync` shape would apply to.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Deleting a project containing an objective, a task (under
   that objective) with a nested checklist (parent + child item), a
   progress note, a status-change history row, a task owner, and a
   meeting leaves zero rows in `Projects`, `Objectives`, `WorkItems`,
   `ChecklistItems`, `ProgressNotes`, `StatusChanges`, `TaskOwners`, and
   `Meetings` for that project, confirmed by direct database inspection
   — matching `GM-024` exactly.
3. **AC3** — Clicking the Delete button opens a dialog showing the exact
   text `"Delete project '{name}' and all its objectives, tasks,
   checklist items and notes? This cannot be undone."` with the
   project's real name interpolated; clicking Cancel makes no
   `DeleteProjectAsync` call and leaves the project, and every row it
   owns, untouched.
4. **AC4** — Clicking the dialog's confirm ("Delete") button calls
   `DeleteProjectAsync`, and the project's row disappears from the list
   with no manual page refresh required.
5. **AC5** — Deleting a project with no objectives, tasks, or meetings at
   all succeeds without error (an empty cascade is still a valid
   cascade).
6. **AC6** — Clicking the Delete icon does not navigate to
   `/projects/{id}`; clicking elsewhere on the same row still does.
7. **AC7** — Exactly nineteen `PlanningService` methods exist (the
   eighteen from before this change, plus `DeleteProjectAsync`).
8. **AC8** — No change to `ProjectDetail.razor`, no page-level "select a
   project" control, and no undo/soft-delete mechanism exists anywhere
   in the diff.

## Edge Cases

- **A project with only some entity types present** (e.g., an objective
  but no tasks, or a task but no meeting) — cascades correctly regardless
  of which subset actually exists; the `.Include` chain targets
  `Tasks.Checklist` specifically because that is the only self-referencing
  relationship in the graph, not because every other relationship needs
  the same treatment.
- **Deleting a project twice in quick succession** (e.g. a stale row from
  a second browser tab) — `DeleteProjectAsync`'s `if (p is null) return;`
  guard (inherited from the legacy body) makes the second call a silent
  no-op, not an exception, matching `task-deletion`'s identical edge case
  for `DeleteTaskAsync`.
- **A project currently open in `ProjectDetail.razor` in another tab/
  session gets deleted from the Projects list.** No live cross-page sync
  exists anywhere else in this rebuild either; the detail page would
  simply fail its next `PlanningService` call against a missing id. Not a
  new gap this item introduces or is expected to close.

## Dependencies

- **Depends on:** `BL-002` (`Objective`), `BL-003` (`WorkItem`), `BL-006`
  (`Meeting`), and transitively `BL-004`/`BL-005`/`BL-007`/`BL-009` — all
  already built. Deleting a project cascades to every row type these
  items introduced.
- **Blocks:** none — this is the last data-model-driven backlog item.

## Notes

No open questions carried over from the proposal — the confirmation text
is quoted verbatim from the real legacy caller, and the cascade-fix
requirement (`.Include(p => p.Tasks).ThenInclude(t => t.Checklist)`) was
confirmed necessary and sufficient by direct reproduction against the
full `GM-024` shape *before* this spec was written, not discovered during
build the way `task-deletion` had to discover its own analogous fix.
