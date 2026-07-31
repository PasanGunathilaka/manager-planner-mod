# Spec: Nested checklist items and grid status badges

**Change:** nested-checklist-items-and-grid-status-badges
**Created:** 2026-07-31
**Status:** 🟡 Draft

## Overview

Fills in `TaskRow.razor`'s last remaining placeholder cell (`<td>&mdash;</td>`)
with the nested `ChecklistItem` tree — already eager-loaded by both
`GetPlannerForProjectAsync` and `GetUngroupedTasksForProjectAsync`, but
never rendered — and adds the two client-side-only status badges
(`OVERDUE`, `⚑ discovered`) to the task cell. One new `PlanningService`
method (`ToggleChecklistItemAsync`, ported exactly from the legacy
source), one new recursive Razor component, and small extensions to
`TaskRow.razor`. Resolves the proposal's two open questions: checklist-
item creation stays deferred (legacy fidelity — no UI ever creates one),
and badges use MudBlazor's semantic palette rather than the legacy's
literal hex colors.

## Requirements

### Functional Requirements

1. **FR1 — `ToggleChecklistItemAsync(itemId, isDone)`.** Ported exactly
   from `../manager-planner/src/ExecutivePlanning.Core/Services/
   PlanningService.cs:161-168`: loads the `ChecklistItem` by id via the
   factory-created context, throws `InvalidOperationException` if not
   found, sets `IsDone = isDone`, and sets `CompletedUtc = isDone ?
   DateTime.UtcNow : null` — DR-011 ("Toggling a checklist item stamps/
   clears its completion time").
2. **FR2 — Nested checklist tree rendered in `TaskRow`'s third `<td>`.**
   Root items (`ParentId == null`) ordered by `SortOrder`; each item's
   own children (`ParentId == item.Id`) likewise ordered by `SortOrder`
   at every level, recursively, to unbounded depth — this matches the
   legacy `BuildTree`'s actual recursive shape
   (`../manager-planner/src/ManagerPlanner.Desktop/ViewModels/
   RowViewModels.cs:72-84`), not a single fixed level. A task with no
   checklist items renders the existing `&mdash;` placeholder unchanged.
3. **FR3 — Tick/untick calls `ToggleChecklistItemAsync` immediately.**
   Each checklist row is a checkbox bound to `IsDone`; toggling it calls
   `ToggleChecklistItemAsync(item.Id, <new value>)` and updates that
   item's `IsDone` locally so the checkbox reflects the new state
   without re-fetching the whole grid (see NFR3 for why no page-wide
   refresh is needed here, unlike `ChangeStatusAsync`).
4. **FR4 — Per-item assignee label, read-only.** When `item.Assignee` is
   non-null, the label is followed by `"— {FullName}"`
   (`ChecklistItemVm.AssigneeText`'s exact format,
   `RowViewModels.cs:32`). **No new `.ThenInclude(c => c.Assignee)` is
   added** for the checklist collection — see Notes for why this
   preserves an exact (if incidental) legacy behavior rather than
   silently improving on it.
5. **FR5 — `OVERDUE` badge.** Shown on the task cell only when
   `WorkItem.Deadline.HasValue && WorkItem.Deadline.Value <
   DateTime.UtcNow && WorkItem.Status != WorkItemStatus.Done` — the
   exact predicate from `RowViewModels.cs:64`, computed client-side in
   `TaskRow`'s `@code` block (matching the file's existing
   `StatusColor`/`StatusText` computed-property pattern), never
   persisted or returned by `PlanningService`.
6. **FR6 — `⚑ discovered` badge.** Shown on the task cell only when
   `WorkItem.IsDiscovered` is `true` (`RowViewModels.cs:62`), same
   client-side, never-persisted treatment as FR5.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** `ToggleChecklistItemAsync` uses
   `IDbContextFactory<PlanningDbContext>` like all ten existing
   `PlanningService` methods — no direct `PlanningDbContext` injection.
2. **NFR2 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: any way to create a new `ChecklistItem`
   (no `AddChecklistItemAsync`, no UI for it); any way to delete a single
   checklist item; any UI that sets/changes a checklist item's assignee;
   progress notes, meeting recording, accountability reporting, or task/
   project deletion (separate, not-yet-built backlog items 7, 6, 8, 9, 10).
3. **NFR3 — No page-wide refresh on toggle.** Unlike `ChangeStatusAsync`
   (whose `StatusChanged` callback bubbles to `ProjectDetail`'s full
   `RefreshAsync`, because it can move a task in/out of the `Overdue`/
   `Done`/etc. counts `ProjectSummary` displays), toggling a checklist
   item's `IsDone` has no effect on any value `ProjectSummary` computes.
   A local, in-place update of the toggled item is sufficient and
   correct — no `EventCallback` bubbling to the parent page is required
   or added for this feature.
4. **NFR4 — Badge styling uses MudBlazor's semantic palette**
   (`Color.Error` for `OVERDUE`, `Color.Warning` for `⚑ discovered`),
   not the legacy's literal hex values (`#b00020`/`#a05a00`) — consistent
   with `ui-modernization`'s system-wide move off literal legacy colors
   (`TaskRow`'s existing `StatusColor` already uses semantic `Color`
   values, not hex).

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Ticking an unticked checklist item persists `IsDone == true`
   and a non-null `CompletedUtc`; unticking a ticked item persists
   `IsDone == false` and `CompletedUtc == null` — confirmed by direct
   inspection of the persisted row. The checkbox reflects the new state
   immediately, with no full page reload.
3. **AC3** — The nested tree renders every level of a checklist's actual
   parent/child structure (not just the first level), each level ordered
   by `SortOrder`, matching a direct read of the same task's
   `ChecklistItem` rows.
4. **AC4** — A checklist item whose `Assignee` navigation is non-null
   after the existing (unmodified) `Include` chain resolves shows
   `"— {FullName}"` after its label; an item whose `Assignee` doesn't
   resolve (including one with a non-null `AssigneeId` that the current
   query shape doesn't happen to fix up — see Notes) shows no assignee
   text. Not every item with a non-null `AssigneeId` in the database is
   required to display a name — matching the legacy app's own actual
   behavior, not an idealized one.
5. **AC5** — `OVERDUE` renders on a task's cell only when that task has a
   `Deadline` in the past and `Status != Done`; it does not render for a
   `Done` task even with a past deadline, and does not render for a task
   with no `Deadline` set.
6. **AC6** — `⚑ discovered` renders on a task's cell only when
   `IsDiscovered == true`.
7. **AC7** — A task with zero checklist items renders the existing
   `&mdash;` placeholder in its third cell, unchanged from before this
   change.
8. **AC8** — No `PlanningService` method exists beyond the ten existing
   ones plus `ToggleChecklistItemAsync` (eleven total) anywhere in the
   diff. No UI control anywhere creates a new checklist item, deletes a
   single checklist item, or edits a checklist item's assignee.
9. **AC9** — Toggling a checklist item does not trigger `ProjectDetail`'s
   `RefreshAsync` and does not change any of the page's displayed
   summary counts (they don't depend on checklist state to begin with).

## Edge Cases

- **A checklist item nested more than one level deep** (a child of a
  child). Must render at its correct depth — the legacy `BuildTree` is
  fully recursive, not capped at one level; this rebuild matches that
  shape exactly (FR2).
- **A checklist item with a non-null `AssigneeId` whose `User` isn't
  independently loaded elsewhere in the same `GetPlannerForProjectAsync`/
  `GetUngroupedTasksForProjectAsync` query result.** Reading the real
  legacy source directly confirms `GetPlannerForProjectAsync` never
  `.ThenInclude`s the checklist's own `Assignee` — the *only* reason
  `ChecklistItemVm.AssigneeText` ever shows a name at all is EF Core's
  automatic relationship fixup: if that same `User` was already tracked
  in the context because they're *also* a task `Assignee` loaded by the
  sibling `.Include(t => t.Assignee)` in the same query, the checklist
  item's `Assignee` reference resolves "for free"; otherwise it stays
  `null` even though `AssigneeId` is set in the database. This is a real,
  verified legacy quirk (not a guess) and this item deliberately
  reproduces it rather than "fixing" it with a new explicit `Include`
  (which the legacy app never had).
- **Toggling a checklist item that was deleted concurrently.** The
  ported `ToggleChecklistItemAsync` throws `InvalidOperationException`
  if the item isn't found, matching legacy exactly — unreachable today
  since no single-checklist-item delete UI exists anywhere (NFR2); not a
  defect to handle specially in this item.
- **A task with an empty `Checklist` collection.** Renders the pre-
  existing `&mdash;` placeholder, not an empty tree component (AC7).

## Dependencies

- **Depends on:** `task-management` (item 3 — `WorkItem`/`ChecklistItem`
  data and its eager-load into `TaskRow` already exist), `task-status-
  transitions` (item 4 — the `OVERDUE` badge's `Status != Done` half of
  its predicate).
- **Blocks:** none directly — item 9 (task deletion) will cascade-delete
  `ChecklistItem` rows regardless of what this item builds; item 11
  (sample-data lifecycle) is the eventual source of checklist rows to
  exercise this feature against in a running app (see Notes).

## Notes

Both of the proposal's open questions are resolved here:

1. **Checklist-item creation stays deferred.** No legacy UI ever calls
   `AddChecklistItemAsync` (functional-spec.md Named Gap #5), and it
   isn't one of this backlog item's own "Maps to capability" bullets.
   Until item 11 (`DbSeeder`'s rebuild equivalent) lands, there is no way
   to create a `ChecklistItem` row through the running app — this
   item's toggle/render logic will be verified against a test fixture or
   a direct DB insert rather than through the UI end-to-end, and that is
   an accepted, explicit consequence of this scoping decision, not an
   oversight.
2. **Badges use MudBlazor's semantic palette**, not the legacy's literal
   hex values — consistent with how `ui-modernization` already treated
   every other visual element in this app.

Single-subtree checklist deletion (the `ChecklistItem.Parent`
self-reference's `Restrict` rule) is explicitly out of scope — per
ADR-0005, "a path the legacy app never runs; no golden master exists — a
human must define intended behaviour," and no capability bullet for item
5 covers deletion in the first place.
