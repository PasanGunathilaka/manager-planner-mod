# Proposal: Nested checklist items and grid status badges

**Created:** 2026-07-31
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

Rebuild-backlog item 5 (`BL-005`) is the only remaining piece of the
Planner Grid's per-task cell that hasn't been built. `TaskRow.razor`
(shipped by `task-status-transitions`) already renders Title, Deadline,
Assignee, and a status chip/buttons — but its third `<td>` is a literal
placeholder:

```razor
<td>&mdash;</td>
```

That's the checklist column. Reading the modern service directly
confirms the data is already flowing to it and going nowhere:
`GetPlannerForProjectAsync` (`src/ManagerPlanner.Core/Services/PlanningService.cs:97-108`)
already does `.Include(o => o.Tasks).ThenInclude(t => t.Checklist)` —
every `WorkItem` handed to `TaskRow` already carries its full
`ChecklistItem` tree, but nothing renders it and nothing can toggle it
(`PlanningService` has no `ToggleChecklistItemAsync` yet). Separately,
the two "visual-only" badges functional-spec.md documents for this same
grid cell — the OVERDUE flag and the "⚑ discovered" flag — don't exist
anywhere in the rebuild either.

Reading the real legacy source directly confirms the exact mechanics
this item ports:

`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:161-168`:
```csharp
/// <summary>Toggles a checklist item's done state and stamps completion.</summary>
public async Task ToggleChecklistItemAsync(int itemId, bool isDone)
{
    var item = await _db.ChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId)
               ?? throw new InvalidOperationException($"Checklist item {itemId} not found.");
    item.IsDone = isDone;
    item.CompletedUtc = isDone ? DateTime.UtcNow : null;
    await _db.SaveChangesAsync();
}
```
This is domain-model.md's DR-011 exactly ("Toggling a checklist item
stamps/clears its completion time").

`../manager-planner/src/ManagerPlanner.Desktop/ViewModels/RowViewModels.cs:43-44,62-64`
— the two badge flags, computed client-side, never persisted or
round-tripped through `PlanningService`:
```csharp
public bool IsOverdue { get; }
public bool IsDiscovered { get; }
...
IsDiscovered = t.IsDiscovered;
IsOverdue = t.Deadline is { } d && d < DateTime.UtcNow && t.Status != WorkItemStatus.Done;
```
And the exact rendered badge text/styling,
`../manager-planner/src/ManagerPlanner.Desktop/Views/PlannerGridView.axaml:73-76`:
```xml
<TextBlock Text="OVERDUE" FontSize="11" FontWeight="Bold" Foreground="#b00020"
           IsVisible="{Binding IsOverdue}" />
<TextBlock Text="⚑ discovered" FontSize="11" Foreground="#a05a00"
           IsVisible="{Binding IsDiscovered}" />
```

The nested-tree shape itself (`ChecklistItem.ParentId`/`SortOrder`,
one level of tick/untick, optional per-item assignee label) is confirmed
by the same file's `BuildTree` (lines 72-84) and `ChecklistItemVm`
(lines 11-34) — a `CheckBox` per node, its own `Label` and, if set, an
`AssigneeText` of `"— {FullName}"`.

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains one method**, ported exactly from the
   legacy source above:
   - `ToggleChecklistItemAsync(itemId, isDone)` — loads the item via the
     established `IDbContextFactory<PlanningDbContext>` pattern (every
     other `PlanningService` method already uses this, e.g.
     `ChangeStatusAsync`), throws `InvalidOperationException` if not
     found (matching the legacy method's own not-found exception
     exactly, not `ValidationException` — this is a lookup failure, not
     a business-rule violation), sets `IsDone`, and stamps/clears
     `CompletedUtc` per DR-011.

2. **`TaskRow.razor`'s third `<td>` renders the nested checklist tree**
   already arriving via `WorkItem.Checklist` (no service change needed
   to fetch it — `GetPlannerForProjectAsync` already includes it). Build
   the same parent/child grouping `RowViewModels.cs.BuildTree` does
   (group by `ParentId`, order each level by `SortOrder`), render one
   level of nesting as an indented list (`MudCheckBox` per item, wired
   to `ToggleChecklistItemAsync`, refreshing the row in place — no page
   reload, matching the immediate-toggle behavior of the legacy
   `TreeView`/`CheckBox` binding). Each item's label is followed by its
   assignee's name when `AssigneeId` is set, matching
   `ChecklistItemVm.AssigneeText`'s `"— {FullName}"` format.

3. **`TaskRow.razor`'s first `<td>`** (Title/Deadline cell) **gains the
   two status badges**, computed client-side exactly as
   `RowViewModels.cs` does — `IsOverdue = WorkItem.Deadline is { } d && d <
   DateTime.UtcNow && WorkItem.Status != WorkItemStatus.Done` and
   `IsDiscovered = WorkItem.IsDiscovered` — shown only when true, with
   text matching the legacy strings ("OVERDUE", "⚑ discovered"). See Open
   Question 2 below for the color/styling fork.

## Scope

### In Scope
- `PlanningService.ToggleChecklistItemAsync(itemId, isDone)` — DR-011,
  including its exact not-found exception behavior
- Rendering the existing (already-fetched) nested `ChecklistItem` tree
  in `TaskRow.razor`'s checklist column, with tick/untick wired to the
  new method
- Per-item assignee label on checklist rows (`"— {FullName}"`), read-only
- The `IsOverdue` and `IsDiscovered` badges on the task cell, computed
  client-side, matching the legacy predicate and label text exactly

### Out of Scope
- **Creating new checklist items.** See Open Question 1 — no legacy UI
  ever calls `AddChecklistItemAsync` (functional-spec.md Named Gap #5:
  "No UI adds a new checklist item... cannot add a new one from either
  app's UI"), and it isn't one of item 5's own "Maps to capability"
  bullets (only tick/untick + badges are). Left for a human call.
- **Deleting a single checklist item** (as opposed to the whole-`WorkItem`
  cascade delete, item 9). The `ChecklistItem.Parent` self-reference's
  `Restrict` rule is real schema behavior, but functional-spec.md Named
  Gap #9 confirms it "is never exercised on its own in the legacy app" —
  there is no golden master for single-subtree delete and no capability
  bullet for it in this item; nothing to port, nothing to build.
- **Editing a checklist item's assignee.** Read-only in the legacy UI too
  (no control sets it) — same never-exposed pattern already established
  for task owners in `planner-grid`/`task-management`.
- **Persisting or exposing the badge flags anywhere outside this one
  render** — legacy computes them purely for display, never stores or
  returns them from `PlanningService`; the rebuild does the same.

## Impact

- **Files affected:** ~2 (estimated) — `PlanningService.cs` (1 new
  method, no new file), `TaskRow.razor` (checklist tree + two badges)
- **Complexity:** small — the data is already being fetched
  (`GetPlannerForProjectAsync` already includes `Checklist`); this item
  is "render it and wire one toggle method," not new plumbing
- **Risk:** low — DR-011's side effect and the badge predicates are
  mechanically exact and directly quoted from the legacy source above;
  the only real forks are the two Open Questions below, neither of
  which blocks the mechanically-specified parts

## Open Questions

1. **Should `AddChecklistItemAsync` be ported now too, even though no
   legacy UI ever calls it?** Without it (and without item 11's
   `DbSeeder`, not yet built), there is currently **no way at all** to
   create a `ChecklistItem` row in the running app — this item's own
   toggle feature would have nothing to exercise it against outside a
   test fixture or a manual DB insert. **Recommended: defer, matching
   legacy fidelity exactly** (tick/untick existing items only, exactly
   what both legacy apps do) — build/verify this item's toggle logic and
   rendering against test fixtures now, and let real checklist rows
   arrive naturally once item 11 (sample-data lifecycle) lands. If
   you'd rather unblock manual testing sooner, say so and I'll add the
   service method now (it's low-risk, DR-004-covered) without adding any
   new UI affordance for it — Named Gap #5 stays undecided either way,
   this would only be an internal seeding hook.
2. **Badge styling: reproduce the legacy's exact hex colors
   (`#b00020` red / `#a05a00` amber), or use MudBlazor's semantic
   palette** (`Color.Error` / `Color.Warning`), matching the pattern
   `TaskRow.razor` already established for its status chip
   (`StatusColor`, `Color.Error` for Blocked, `Color.Success` for Done,
   etc.)? **Recommended: MudBlazor semantic colors**, for the same
   reason `ui-modernization` moved the whole app off literal legacy hex
   values — text content ("OVERDUE", "⚑ discovered") is the actual
   legacy-fidelity requirement here per functional-spec.md; the exact
   RGB values were an artifact of the old Win95-style skin `ui-
   modernization` already decided to discard (CQ-006), not a business
   rule.

---

**To proceed:** Review this proposal and approve to begin planning.
