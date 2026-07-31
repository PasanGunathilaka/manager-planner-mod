# Tasks: Nested checklist items and grid status badges

**Change:** nested-checklist-items-and-grid-status-badges
**Created:** 2026-07-31
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) add `ToggleChecklistItemAsync` to
`PlanningService`, (2) build the recursive `ChecklistTree` component,
wire it into `TaskRow`'s empty checklist cell, add the two OVERDUE/
discovered badges, then verify end-to-end. No task adds checklist-item
creation, single-item deletion, or assignee editing — those stay out of
scope per spec.md NFR2/AC8.

## Tasks

### Wave 1 — Core business logic

- [x] `T1` — Add `ToggleChecklistItemAsync` to `PlanningService`
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at
    `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs:161-168`,
    not the doc summary. `ToggleChecklistItemAsync(int itemId, bool
    isDone)`: load the `ChecklistItem` by id via the factory-created
    context (`FirstOrDefaultAsync`, throw `InvalidOperationException`
    with message `$"Checklist item {itemId} not found."` if not found —
    matches legacy exactly); set `item.IsDone = isDone;` then
    `item.CompletedUtc = isDone ? DateTime.UtcNow : null;` (DR-011 —
    both directions, unconditional); `SaveChangesAsync()`. Add it as the
    eleventh method, after `ChangeStatusAsync`, using
    `IDbContextFactory<PlanningDbContext>` like all ten existing methods.

### Wave 2 — UI + verification

- [x] `T2` — `ChecklistTree` component, wired into `TaskRow`, plus OVERDUE/discovered badges, end-to-end verification
  - Files: `src/ManagerPlanner.Web/Components/Pages/ChecklistTree.razor`, `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: Before writing markup, confirm `MudCheckBox<bool>`'s exact
    parameter names (`Value`/`ValueChanged`, `Dense` availability)
    against the installed MudBlazor 9.7.0 package (design.md Risk —
    same reflection-probe technique `ui-modernization` used for
    `MudSelect`/`MudDatePicker`/etc.).

    Create `ChecklistTree.razor` in `Components/Pages/` (flat layout,
    matching `TaskRow.razor`'s own location): `@inject PlanningService
    PlanningService`; `[Parameter] public List<ChecklistItem> Items {
    get; set; } = new();`. For each item in `Items` (already ordered by
    the caller), render a `MudCheckBox<bool>` bound to `item.IsDone`
    (via `Value`/`ValueChanged`, not `@bind-Value`, since the change
    handler must call the service) showing `item.Label` followed by
    `$" — {item.Assignee.FullName}"` only when `item.Assignee` is
    non-null (do **not** add any new `Include`/`ThenInclude` anywhere to
    make this resolve more often — design.md Key Decision 2, this is a
    deliberate legacy-fidelity gap, not a bug to fix). If `item.Children.
    Count > 0`, recurse into another `<ChecklistTree Items="item.
    Children.OrderBy(c => c.SortOrder).ToList()" />` wrapped in an
    indented container (e.g. a `<div style="margin-left:1.5rem">`) —
    unbounded depth, not a hard-coded two levels (design.md Key Decision
    1). The toggle handler: `await PlanningService.
    ToggleChecklistItemAsync(item.Id, newValue); item.IsDone =
    newValue;` — no `EventCallback` to a parent, no page refresh
    (spec.md NFR3/design.md Key Decision 3).

    In `TaskRow.razor`: replace the third `<td>&mdash;</td>` with `@if
    (WorkItem.Checklist.Any(c => c.ParentId == null)) { <ChecklistTree
    Items="WorkItem.Checklist.Where(c => c.ParentId == null).OrderBy(c
    => c.SortOrder).ToList()" /> } else { <text>&mdash;</text> }`. In
    the existing title/deadline cell, add two computed properties next
    to the file's existing `StatusText`/`StatusColor`: `IsOverdue =>
    WorkItem.Deadline.HasValue && WorkItem.Deadline.Value <
    DateTime.UtcNow && WorkItem.Status != WorkItemStatus.Done;` and
    `IsDiscovered => WorkItem.IsDiscovered;`. Render two badges
    conditionally in that cell — `OVERDUE` (`MudText`/`MudChip`,
    `Color.Error`, bold) shown only `@if (IsOverdue)`, and `⚑ discovered`
    (`Color.Warning`) shown only `@if (IsDiscovered)` — text matching
    the legacy strings exactly (spec.md FR5/FR6, design.md Key Decision
    4 — semantic MudBlazor colors, not the legacy's literal hex).

    Verify: `dotnet build` (AC1). Since no checklist-item-creation UI
    exists anywhere yet (spec.md Notes), seed at least one `WorkItem`
    with a multi-level `ChecklistItem` tree via a direct EF Core insert
    (a scratch console app against the live SQLite file, or a temporary
    in-test arrange) — confirm the rendered tree matches that structure
    exactly, including a grandchild-level item (AC3); confirm the
    assignee-text behavior matches AC4's exact framing (present only
    when the fixup resolves, not for every `AssigneeId`); tick and
    untick an item and confirm via direct DB inspection that `IsDone`/
    `CompletedUtc` persist correctly in both directions with no full
    page reload (AC2); confirm a task with zero checklist items still
    shows the plain `&mdash;` (AC7); confirm `OVERDUE`/`⚑ discovered`
    render/don't render per AC5/AC6 across a past-deadline-but-Done
    task, a past-deadline-not-Done task, a future-deadline task, and a
    discovered vs. non-discovered task; confirm no `RefreshAsync` call
    or summary-count change follows a checklist toggle (AC9). Use
    `form_input`/JS dispatch (`element.click()`) per
    `.specclaw/context.md`'s documented fallback for claude-in-chrome
    interactions, and `read_page` immediately before every click.

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
