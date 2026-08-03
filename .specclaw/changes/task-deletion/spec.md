# Spec: Task deletion (cascade)

**Change:** task-deletion
**Created:** 2026-08-03
**Status:** 🟡 Draft

## Overview

Adds the ability to permanently delete a task, cascading to everything it
owns — checklist items, progress notes, status history, and task owners —
via a confirmation dialog on each `TaskRow`. This is the first backlog
item that removes data rather than adding it, the first feature in this
rebuild to actually use the `IDialogService` infrastructure
`ui-modernization` wired but never exercised, and requires zero schema or
migration changes: every cascade relationship this item depends on
(`WorkItem`→`ProgressNote`/`StatusChange`/`ChecklistItem` Cascade,
`TaskOwner` Cascade on both FKs) already exists from
`scaffold-blazor-solution`'s `InitialCreate` migration.

## Requirements

### Functional Requirements

1. **FR1 — `DeleteTaskAsync(taskId)`.** Same observable contract as
   `../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:73-79`
   (find, no-op if missing, remove, save), **but not a byte-for-byte port**
   — it must load `.Include(w => w.Checklist)` before removing. Confirmed
   by direct reproduction: a plain `FindAsync` + `Remove` (the legacy's
   literal body) throws `SQLite Error 19: FOREIGN KEY constraint failed`
   against a fresh `IDbContextFactory`-created context whenever the task
   has a nested checklist item (a parent plus a child) — every call in
   this app gets a fresh context (ADR-0002), so this is not an edge case,
   it is the primary scenario `GM-025`/AC2 requires. The legacy method
   works in the desktop app only because its `PlanningService` holds one
   long-lived `DbContext` for the whole session, so entities added earlier
   in that session are already tracked when delete runs — EF Core's own
   client-side cascade (which orders a self-referencing tree correctly)
   takes over instead of SQLite's raw DB-level cascade, which cannot
   safely resolve `ChecklistItem.ParentId`'s `Restrict` self-reference
   *and* `WorkItemId`'s `Cascade` from a cold, untracked context (this is
   exactly the failure mode the schema comment "Restrict (children
   removed in app code) to avoid multiple cascade paths on SQLite"
   describes — for this rebuild's per-call-fresh-context architecture,
   "removed in app code" must mean the `.Include`, not an assumption the
   DB alone will sort it out). `ProgressNote`/`StatusChange`/`TaskOwner`
   have no such self-reference and cascade correctly from a cold context
   without needing to be included.
2. **FR2 — A "Delete" icon button on each `TaskRow`**, in a new, minimal
   "Actions" cell — this rebuild has no "select a task first" concept
   anywhere (every prior item — status, checklist, notes — already acts
   on a row directly), so the button targets that row's own task.
3. **FR3 — A confirmation dialog via MudBlazor's `IDialogService`**,
   reproducing the real legacy text verbatim, confirmed by reading
   `../manager-planner/src/ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:233-247`:
   `"Delete task '{title}' and its checklist and notes?\nThis cannot be
   undone."` Uses `IDialogService.ShowMessageBoxAsync(string title, string
   message, string yesText = "OK", string? noText = null, string?
   cancelText = null, DialogOptions? options = null)` — confirmed via
   reflection against the installed `MudBlazor.dll` (9.7.0) to be the
   correct three-overload signature — called with `yesText: "Delete"`,
   `cancelText: "Cancel"` (no `noText`, matching the legacy's simple
   Yes/Cancel shape, not a three-way choice).
4. **FR4 — On confirm, call `DeleteTaskAsync` then raise a new
   `TaskDeleted` `EventCallback`**, wired (in `ProjectDetail.razor`) to
   the page's existing full `RefreshAsync` — the same reuse-the-full-
   refresh shape already established for `StatusChanged`/`NoteAdded`,
   needed here because a deleted task must disappear from the
   objective/ungrouped lists *and* the Accountability rows, not just some
   row-local state.
5. **FR5 — Canceling the dialog makes no service call.** `DeleteTaskAsync`
   is only invoked when `ShowMessageBoxAsync` returns `true`.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** `DeleteTaskAsync` uses
   `IDbContextFactory<PlanningDbContext>` like all seventeen existing
   `PlanningService` methods.
2. **NFR2 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: project deletion (backlog item BL-010, not
   yet built); a `DeleteUserAsync` method or any user-management UI; any
   undo/soft-delete mechanism; a page-level "select a task" flow.
3. **NFR3 — Reuse the existing full-refresh callback shape.** `TaskRow`'s
   new `TaskDeleted` callback is wired to `ProjectDetail`'s existing
   `RefreshAsync`, not a new lighter method, consistent with
   `StatusChanged`/`NoteAdded`.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Deleting a task that has a nested checklist (a parent item
   plus a child item), a progress note, a status-change history row, and
   an owner leaves zero rows in `WorkItems`, `ChecklistItems`,
   `ProgressNotes`, and `TaskOwners` for that task, confirmed by direct
   database inspection — matching `GM-025` exactly.
3. **AC3** — Clicking the Delete button opens a dialog showing the exact
   text `"Delete task '{title}' and its checklist and notes? This cannot
   be undone."` with the task's real title interpolated; clicking Cancel
   makes no `DeleteTaskAsync` call and leaves the task, and every row it
   owns, untouched.
4. **AC4** — Clicking the dialog's confirm ("Delete") button calls
   `DeleteTaskAsync`, and the task's row disappears from its
   objective/ungrouped section, and the Accountability section's rows
   update to no longer include it, with no manual page refresh required.
5. **AC5** — Deleting a task with no checklist items, no notes, no status
   history, and no owners succeeds without error (an empty cascade is
   still a valid cascade).
6. **AC6** — The `TaskOwner`→`User` cascade (`GM-033`'s scope) is
   confirmed already correctly configured as `DeleteBehavior.Cascade` in
   `PlanningDbContext.OnModelCreating` — verified by reading the existing
   schema configuration directly, not by adding a new service method
   (this item adds no user-deletion capability).
7. **AC7** — Exactly eighteen `PlanningService` methods exist (the
   seventeen from before this change, plus `DeleteTaskAsync`).
8. **AC8** — No project-deletion UI, no `DeleteUserAsync` method, and no
   undo/soft-delete mechanism (e.g. an `IsDeleted` flag) exists anywhere
   in the diff.

## Edge Cases

- **The task's `Objective` survives deletion.** `Objective`→`WorkItem` is
  the FK direction that matters when an *Objective* is deleted (`SetNull`
  on its child tasks); deleting a task itself never touches its parent
  `Objective` row at all.
- **Deleting the only task in the "Ungrouped" section.** The section's
  existing conditional (`_ungroupedTasks is { Count: > 0 }`) already
  hides it once empty — no new empty-state UI needed.
- **Deleting a task that is the sole remaining "at-risk" row in
  Accountability.** The section's existing empty-state ("No tasks yet.")
  already covers a now-empty project.
- **Deleting a task twice in quick succession** (e.g. a stale row from a
  second browser tab) — `DeleteTaskAsync`'s `if (t is null) return;` guard
  makes the second call a silent no-op, not an exception.

## Dependencies

- **Depends on:** `BL-003` (`WorkItem` itself), `BL-004`
  (`StatusChange` history), `BL-005` (`ChecklistItem` tree), and `BL-007`
  (`ProgressNote`) — all already built. Deleting a task cascades to every
  row type these items introduced.
- **Blocks:** none — `BL-010` (Project deletion) is a separate,
  independent cascade path (`Project`→everything), not dependent on this
  item. **However**, `BL-010`'s own `DeleteProjectAsync` will cascade
  through `WorkItem` to the same self-referencing `ChecklistItem` tree
  this item just discovered breaks from a cold context — its future
  proposal/design must account for the same `.Include` requirement (one
  level deeper: `Project` → `WorkItem` → `ChecklistItem`), not assume a
  plain `FindAsync` + `Remove` is safe there either.

## Notes

One open question from the proposal needed revision during planning, not
carried forward as originally stated: the proposal assumed `DeleteTaskAsync`
would be a byte-for-byte port. Direct reproduction against this rebuild's
actual `IDbContextFactory`-per-call architecture proved that assumption
wrong — see FR1 for the full explanation and fix (`.Include(w =>
w.Checklist)`). The confirmation text is still quoted verbatim from the
real legacy caller, and `GM-033`'s `TaskOwner`→`User` cascade is still
confirmed already correctly configured in the existing schema, requiring
no new service method since this rebuild has no user-deletion feature to
build — those two conclusions stand as proposed.
