# Proposal: Task status transitions and the StatusChange audit trail

**Created:** 2026-07-28
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

`WorkItem.Status` currently only ever holds its entity default,
`NotStarted` — `task-management` shipped task creation and viewing, but
nothing in the rebuild can change a task's status yet. Per domain-model.md,
`StatusChange` is "an immutable audit record of a task status transition"
giving "the Manager a defensible history of when work actually moved
forward (or stalled)" — without this item, that audit trail can never be
populated, and every task-row's status text (already rendered by
`TaskRow.razor`) is permanently stuck at "Not started."

Rebuild-backlog item 4 merges three legacy surfaces — Executive Planning
Desktop's four status buttons, Manager Planner Desktop's "Task ▸ Mark
selected Done" menu item, and its "✔ Mark Done" toolbar/Task+Notes button
— since all three call the same `PlanningService.ChangeStatusAsync`
method and produce the same `StatusChange` row / `CompletedUtc` side
effect. They differ only in **how many** of the four `WorkItemStatus`
values each UI exposes — a fidelity nuance this proposal surfaces as an
open question below rather than silently picking one.

Reading the real legacy source directly confirms the exact mechanics:
`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:184-205`:

```csharp
public async Task ChangeStatusAsync(int taskId, WorkItemStatus newStatus, int changedById, string? reason = null)
{
    var task = await _db.WorkItems.FirstOrDefaultAsync(t => t.Id == taskId)
               ?? throw new InvalidOperationException($"Task {taskId} not found.");

    if (task.Status == newStatus) return;

    var change = new StatusChange
    {
        WorkItemId = task.Id,
        FromStatus = task.Status,
        ToStatus = newStatus,
        ChangedById = changedById,
        Reason = reason
    };

    task.Status = newStatus;
    task.CompletedUtc = newStatus == WorkItemStatus.Done ? DateTime.UtcNow : null;

    _db.StatusChanges.Add(change);
    await _db.SaveChangesAsync();
}
```

This confirms domain-model.md's Business Rule 8 (same-status change is a
no-op, no audit row — the `if (task.Status == newStatus) return;` guard)
and Business Rule 9 (`CompletedUtc` set on `Done`, cleared otherwise — both
directions, unconditionally) exactly as stated, with nothing further to
verify: both rules are "fully specified mechanically and covered by named
tests quoted in domain-model.md" (`ChangeStatus_to_same_status_is_noop`),
so no golden-master capture is needed for this item.

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains one method**, ported exactly from the legacy
   source above:
   - `ChangeStatusAsync(taskId, newStatus, changedById, reason = null)` —
     no-op guard, writes a `StatusChange` row (`FromStatus`/`ToStatus`/
     `ChangedById`), updates `WorkItem.Status`, and sets/clears
     `CompletedUtc` exactly per the legacy code above. Uses the
     established `IDbContextFactory<PlanningDbContext>` pattern like every
     other `PlanningService` method. `changedById` is supplied via the
     already-existing `GetCurrentManagerIdAsync()` stand-in — the same
     mechanism the legacy apps use for `_currentUserId`, so no new
     "current user" concept is introduced here.

2. **`TaskRow.razor` gains inline status controls** — the natural
   web-native home for them, since the rebuild has no "select a task
   first, then act on it" concept yet (that's rebuild-backlog item 7's
   job; `task-management`'s proposal explicitly deferred click-through
   task selection). Each row's existing Owner/status cell gains buttons
   for the `WorkItemStatus` values the fork below settles on, calling
   `ChangeStatusAsync` directly against that row's own task and refreshing
   in place — no page reload, no confirmation dialog (neither legacy
   surface shows one for a status change, unlike task/project deletion).

## Scope

### In Scope
- `PlanningService.ChangeStatusAsync(taskId, newStatus, changedById, reason = null)`
- Status-change buttons on each `TaskRow`, wired to `ChangeStatusAsync`
- The `StatusChange` audit row and `CompletedUtc` side effect, exactly per
  Business Rules 8 and 9
- The no-op guard (same-status "change" writes nothing)

### Out of Scope
- **A `Reason` input.** Reading both legacy call sites directly
  (`MainWindowViewModel.SetStatusAsync` and `ManagerPlanner.Desktop`'s
  `MarkDone`) confirms neither ever supplies one — `ChangeStatusAsync` is
  always called with `reason` omitted. `StatusChange.Reason` stays in the
  schema, settable only by a future item, matching the same
  never-exposed-in-any-legacy-UI pattern already established for
  `Objective.KeyResult` in `planner-grid`.
- **Viewing the `StatusChange` history itself** (an audit *trail* view) —
  no legacy UI surfaces this list anywhere; domain-model.md's "immutable
  audit record... gives the Manager a defensible history" describes the
  *data's* purpose, not an existing read surface. Recording history without
  a view for it yet is consistent with this rebuild's item-by-item
  sequencing (the Accountability report, item 8, is the eventual consumer).
- **Task deletion, checklist, badges, notes, meetings** — separate,
  not-yet-built backlog items (5, 7, 9, 6).
- **A confirmation dialog before changing status** — neither legacy
  surface shows one; only task/project deletion (items 9/10) do.

## Impact

- **Files affected:** ~2 (estimated) — `PlanningService.cs` (1 new
  method, no new file), `TaskRow.razor` (extended with status buttons)
- **Complexity:** small — a single, already-precisely-specified service
  method plus a small UI addition to an existing component
- **Risk:** low — both business rules are mechanically exact and
  test-covered in the legacy source (no golden-master capture needed);
  the only open design question is the button-count fork below

## Open Questions

1. **Expose all four `WorkItemStatus` values as buttons on every row, or
   preserve a Done-only shortcut somewhere?** This is the fork
   rebuild-backlog item 4 flags: Executive Planning Desktop exposes
   "Not started"/"In progress"/"Blocked"/"Mark done" as four buttons;
   Manager Planner Desktop's Planner Grid (the surface this rebuild's
   `TaskRow` actually descends from) exposes **only** "✔ Mark Done" — no
   legacy UI here ever shows a path back to `NotStarted`/`InProgress`/
   `Blocked`. Neither legacy document states this is intentional; it
   needs your call. **Recommended: expose all four**, since (a) every
   button calls the same one-line `ChangeStatusAsync` call regardless of
   which value is chosen — there's no extra service complexity to "save"
   by omitting three of four, and (b) a Done-only affordance in a rebuild
   that already tracks `Blocked`/`InProgress` would make those values
   permanently unreachable through any UI, which reads as an accidental
   regression rather than a deliberate simplification. If you'd rather
   match Manager Planner Desktop's Done-only surface exactly (treating the
   four-button surface as Executive Planning Desktop-specific and *not*
   ported), say so and I'll scope `TaskRow` to a single "Mark done" button
   instead.
2. **Button labels/order.** If four buttons are built, recommend matching
   Executive Planning Desktop's exact order and labels ("Not started" /
   "In progress" / "Blocked" / "Mark done") for consistency with the one
   legacy surface that already shows all four.

---

**To proceed:** Review this proposal and approve to begin planning.
