# Spec: Task status transitions and the StatusChange audit trail

**Change:** task-status-transitions
**Created:** 2026-07-28
**Status:** 🟡 Draft

## Overview

Adds the ability to change a task's status and records an immutable
`StatusChange` audit row for every real transition. One new
`PlanningService` method, ported exactly from the real legacy source, plus
four status buttons added to the existing `TaskRow.razor` component (built
by `task-management`). This resolves the proposal's open question by
exposing all four `WorkItemStatus` values on every row, matching Executive
Planning Desktop's fuller surface rather than Manager Planner Desktop's
Done-only shortcut.

## Requirements

### Functional Requirements

1. **FR1 — `ChangeStatusAsync(taskId, newStatus, changedById, reason =
   null)`.** Ported exactly from the real legacy `AddTaskAsync`'s
   neighbor (`../manager-planner/src/ExecutivePlanning.Core/Services/
   PlanningService.cs:184-205`): loads the `WorkItem`; if
   `task.Status == newStatus`, returns with no further effect (no
   `StatusChange` row, no `SaveChangesAsync` call) — Business Rule 8.
   Otherwise creates a `StatusChange` (`WorkItemId`, `FromStatus` = the
   task's status before the change, `ToStatus` = `newStatus`,
   `ChangedById`, `Reason`), sets `task.Status = newStatus`, and sets
   `task.CompletedUtc = newStatus == WorkItemStatus.Done ? DateTime.UtcNow
   : null` unconditionally in both directions — Business Rule 9.
2. **FR2 — Four status buttons on every `TaskRow`.** Labeled "Not
   started" / "In progress" / "Blocked" / "Mark done", matching Executive
   Planning Desktop's exact order (`MainWindow.axaml:117-124`). Always
   rendered and clickable regardless of the row's current status — no
   button is hidden or disabled for the current value (matches legacy;
   the no-op guard in FR1 makes a redundant click harmless rather than
   needing UI-level prevention).
3. **FR3 — `changedById` resolved fresh per click** via the existing
   `PlanningService.GetCurrentManagerIdAsync()` stand-in — the same
   mechanism `AddProjectAsync`'s page-level caller already uses, not a new
   "current user" concept.
4. **FR4 — No confirmation dialog.** Clicking a status button takes
   effect immediately — matches both legacy surfaces exactly; unlike task/
   project deletion (items 9/10), no legacy status-change UI shows a
   confirmation.
5. **FR5 — Grid refresh after a status change.** The row's `TaskRow`
   raises a parameterless `StatusChanged` `EventCallback` after a
   successful `ChangeStatusAsync` call; `ProjectDetail.razor` wires this to
   its existing `RefreshAsync()` method (already reloads the summary,
   objectives, team members, and ungrouped tasks) — chosen specifically so
   the summary's `Done`/`InProgress`/`Blocked`/`NotStarted`/`Overdue`
   counts update immediately, not just the row itself.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** `ChangeStatusAsync` uses
   `IDbContextFactory<PlanningDbContext>` like every existing
   `PlanningService` method — no direct `PlanningDbContext` injection.
2. **NFR2 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: a `Reason` input field or any non-null
   `Reason` ever written by this UI; a view of the `StatusChange` audit
   history itself; a confirmation dialog before a status change; task
   deletion, checklist rendering, OVERDUE/discovered badges, progress
   notes, or meeting recording (separate, not-yet-built backlog items 9,
   5, 7, 6).
3. **NFR3 — No per-status button disabling.** All four buttons remain
   visible and clickable on every row regardless of that row's current
   `Status` — matches legacy (no disabling logic exists in either
   ViewModel/View read), and is not a gap this item needs to fix.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Clicking a status button whose value differs from the task's
   current status persists `WorkItem.Status == <clicked value>` and
   creates exactly one new `StatusChange` row (`FromStatus` = the prior
   status, `ToStatus` = the clicked value, `ChangedById` = the bootstrapped
   Manager's id, `Reason == null`, `ChangedUtc` set) — confirmed by direct
   inspection of the persisted rows. The page updates in place, no full
   reload.
3. **AC3** — Clicking "Mark done" sets `WorkItem.CompletedUtc` to a
   non-null UTC timestamp. Subsequently clicking any of the other three
   buttons on that same (now-Done) task clears `CompletedUtc` back to
   `null`.
4. **AC4** — Clicking the button matching a task's current status is a
   no-op: `WorkItem.Status` is unchanged and no new `StatusChange` row is
   created (row count before == row count after, confirmed by direct DB
   inspection).
5. **AC5** — All four buttons ("Not started" / "In progress" / "Blocked" /
   "Mark done") render on every task row, in both the per-objective
   sections and the Ungrouped section, regardless of that task's current
   status.
6. **AC6** — After a status change, the Project Detail page's summary
   counts (Total/Done/In progress/Blocked/Not started/Overdue) reflect the
   new state without the user clicking the separate "Refresh" button.
7. **AC7** — No `Reason` input field exists anywhere in the rendered
   output; every `StatusChange` row created through this UI has
   `Reason == null`.
8. **AC8** — No `PlanningService` method exists beyond the nine existing
   ones (`GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`,
   `GetCurrentManagerIdAsync`, `AddObjectiveAsync`,
   `GetPlannerForProjectAsync`, `AddTaskAsync`, `GetTeamMembersAsync`,
   `GetUngroupedTasksForProjectAsync`) plus `ChangeStatusAsync` added here
   (ten total) — anywhere in the diff.
9. **AC9** — No confirmation dialog, browser `confirm()`, or modal appears
   between clicking a status button and the change taking effect.

## Edge Cases

- **A task with zero prior `StatusChange` rows.** The first transition
  creates the first row correctly (`FromStatus` = the entity default,
  `NotStarted`, unless already changed).
- **Repeatedly clicking the same (current) status.** Each click is an
  independent no-op per FR1/AC4 — no accumulation of redundant rows, no
  error.
- **Multiple sequential transitions on the same task.** Each real
  transition appends its own `StatusChange` row; the audit trail is
  additive and never rewritten (`StatusChange` remains immutable — no
  update/delete path exists for it, matching domain-model.md's "immutable
  audit record" description).
- **A task deleted concurrently with a status-change click.** The ported
  `ChangeStatusAsync` throws `InvalidOperationException` if the task
  isn't found (matching legacy exactly) — unreachable today since task
  deletion (item 9) doesn't exist yet in this rebuild; not a defect to
  handle specially in this item.

## Dependencies

- **Depends on:** `task-management` (item 3 — a `WorkItem` and its
  `TaskRow` rendering must exist before status buttons can attach to it).
- **Blocks:** item 8 (Accountability reporting reads `WorkItem.Status`/
  `CompletedUtc`), item 9 (task deletion cascades `StatusChange` rows).

## Notes

The proposal's open question is resolved here: `TaskRow` exposes all four
`WorkItemStatus` values as buttons (Executive Planning Desktop's fuller
surface), not Manager Planner Desktop's Done-only shortcut — every button
costs the same one-line `ChangeStatusAsync` call, and a Done-only surface
would make `Blocked`/`InProgress` permanently unreachable through any UI in
this rebuild.
