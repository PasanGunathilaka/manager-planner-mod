# Design: Task status transitions and the StatusChange audit trail

**Change:** task-status-transitions
**Created:** 2026-07-28

## Technical Approach

1. Add one method to the existing `ManagerPlanner.Core/Services/
   PlanningService.cs` (no new file): `ChangeStatusAsync`, ported
   verbatim from the real legacy source, following the established
   `IDbContextFactory<PlanningDbContext>` pattern (open/dispose a
   short-lived context per call).
2. Extend the existing `TaskRow.razor` (built by `task-management`):
   inject `PlanningService`, add a parameterless `StatusChanged`
   `EventCallback` parameter, and render four buttons in the row's
   existing Owner/status cell. Each button's click handler resolves
   `changedById` via `GetCurrentManagerIdAsync()`, calls
   `ChangeStatusAsync`, then invokes `StatusChanged`.
3. Extend `ProjectDetail.razor`: wire `StatusChanged="RefreshAsync"` on
   both existing `<TaskRow>` usages (the per-objective loop and the
   Ungrouped section) — no new refresh method, reusing the one already
   there so the summary counts stay consistent too.

No entity, schema, or migration changes — `StatusChange` and
`WorkItem.CompletedUtc` already exist from `scaffold-blazor-solution`.

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── ... nine existing methods ...
└── ChangeStatusAsync(taskId, newStatus, changedById, reason = null)  [new]

src/ManagerPlanner.Web/Components/Pages/
├── TaskRow.razor                               (extended)
│   ├── existing: title+deadline | assignee+status | checklist placeholder
│   ├── new: @inject PlanningService
│   ├── new: [Parameter] public EventCallback StatusChanged { get; set; }
│   └── new: four status buttons, each →
│            GetCurrentManagerIdAsync() → ChangeStatusAsync → StatusChanged.InvokeAsync()
└── ProjectDetail.razor                         (extended)
    └── both <TaskRow> call sites gain StatusChanged="RefreshAsync"
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `ChangeStatusAsync(taskId, newStatus, changedById, reason = null)` |
| `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` | Modify | + `PlanningService` injection, + `StatusChanged` `EventCallback` parameter, + four status buttons |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | Both `<TaskRow>` call sites gain `StatusChanged="RefreshAsync"` |

## Data Model Changes

None. `StatusChange`, `WorkItem.Status`, and `WorkItem.CompletedUtc`
already exist from `scaffold-blazor-solution`'s migration.

## API Changes

None. No HTTP/JSON API — `TaskRow` calls `PlanningService` directly, as
established in every prior change.

## Key Decisions

1. **All four `WorkItemStatus` values are exposed as buttons** — resolves
   the proposal's open question in favor of Executive Planning Desktop's
   fuller surface over Manager Planner Desktop's Done-only shortcut.
   Every button costs the same one-line `ChangeStatusAsync` call, and a
   Done-only rebuild would make `Blocked`/`InProgress` permanently
   unreachable through any UI — read as an accidental regression, not a
   deliberate simplification.
2. **Button order/labels match Executive Planning Desktop exactly**:
   "Not started" / "In progress" / "Blocked" / "Mark done"
   (`MainWindow.axaml:117-124`) — the one legacy surface that already
   shows all four, so there's a real precedent to match rather than
   inventing new wording.
3. **No `Reason` input** — confirmed by reading both legacy call sites
   directly (`ExecutivePlanning.Desktop`'s `SetStatusAsync` and
   `ManagerPlanner.Desktop`'s `MarkDone`): neither ever supplies one.
   `StatusChange.Reason` stays in the schema, unset by this item — same
   treatment `planner-grid` gave `Objective.KeyResult`.
4. **`StatusChanged` triggers the existing `RefreshAsync`, not a new
   lighter method.** A status change moves a task in/out of `Done` and
   in/out of `Overdue`-relevant states, both of which the summary counts
   already track — reusing the full refresh keeps `ProjectDetail`'s
   displayed counts correct immediately, rather than shipping a feature
   that visibly makes its own summary stale until a manual refresh.
5. **`changedById` resolved fresh per click via `GetCurrentManagerIdAsync()`,
   not cached on the component.** Matches the existing pattern in
   `Projects.razor`'s `AddProjectAsync` handler (fetches fresh on every
   submit) — not a new pattern introduced here.
6. **No per-status button disabling.** All four buttons stay visible and
   clickable regardless of the row's current status, matching legacy (no
   disabling logic exists in either ViewModel/View read) — the no-op
   guard in `ChangeStatusAsync` itself is what makes a redundant same-
   status click harmless, not a UI-level prevention.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — `StatusChange` entity fields,
  Business Rule 8 ("Changing a task to its current status is a no-op...
  before any `StatusChange` row is written"), Business Rule 9
  ("Completion timestamp tracks the Done transition, both ways").
- **Real legacy source**, read directly:
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:184-205` — the exact `ChangeStatusAsync`
    body (grounds FR1 and Key Decisions 3/4's fidelity).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Desktop\
    Views\MainWindow.axaml:117-124` — the four status buttons' exact
    labels/order (grounds FR2/Key Decision 2).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Desktop\
    ViewModels\MainWindowViewModel.cs:200-209` (`SetStatusAsync`) — never
    passes a `reason` argument (grounds Key Decision 3).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    ViewModels\MainViewModel.cs:176-183` (`MarkDone`) and
    `Views\MainWindow.axaml:25,45` / `Views\TaskNotesView.axaml:16` — the
    Done-only surface this change deliberately doesn't limit itself to
    (grounds Key Decision 1).
- `.specclaw/context.md` — the `IDbContextFactory` pattern (NFR1) and the
  established `GetCurrentManagerIdAsync()` "current user" stand-in
  (FR3/Key Decision 5).

## Risks & Mitigations

- **Risk:** exposing all four buttons deviates from Manager Planner
  Desktop's Done-only surface — the app this rebuild's Planner Grid most
  directly descends from. **Mitigation:** an explicit, proposal-approved
  decision (Key Decision 1), not a silent choice; recorded here and in
  spec.md's Notes.
- **Risk:** no confirmation before a status change could feel abrupt in a
  web UI compared to a desktop app. **Mitigation:** matches legacy
  exactly, and a status change is reversible (unlike deletion, which does
  get a confirmation in items 9/10 — a deliberate distinction, not an
  inconsistency).
- **Risk:** `StatusChange` rows accumulate with no view of the audit
  trail itself in this item. **Mitigation:** explicitly out of scope
  (NFR2) — item 8 (Accountability reporting) is the eventual consumer of
  this data, not this item.
