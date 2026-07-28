# Design: Task (WorkItem) creation and viewing

**Change:** task-management
**Created:** 2026-07-28

## Technical Approach

1. Add three methods to the existing `ManagerPlanner.Core/Services/
   PlanningService.cs` (no new file): `AddTaskAsync` (ported from the real
   legacy source, minus `discoveredInMeetingId`), `GetTeamMembersAsync`
   (ported exactly), and `GetUngroupedTasksForProjectAsync` (new, no
   legacy equivalent). Add `.AsSplitQuery()` to the existing
   `GetPlannerForProjectAsync` — a follow-up `.specclaw/context.md`
   already flagged for "item 3+." All follow the established
   `IDbContextFactory<PlanningDbContext>` pattern.
2. Extract a small `TaskRow.razor` component (`Components/Pages/`, next to
   `ProjectDetail.razor`) rendering the 3-column task-row markup (task
   cell: title + deadline; owner/status cell: assignee-or-"Unassigned" +
   status text; checklist cell: empty placeholder). This mirrors the
   legacy source's own row concept (`TaskRowVm` in `RowViewModels.cs`, its
   own `DataTemplate` in `PlannerGridView.axaml`) and is used from two
   render sites in this change (per-objective loop, Ungrouped section) —
   not speculative, a concrete duplication-avoidance need introduced by
   this same change.
3. Extend `ProjectDetail.razor`'s existing Planner Grid section:
   - One unified "Add task" form (title, objective select, assignee
     select, deadline date input, description textarea, discovered
     checkbox) — replacing each objective's "No tasks yet." placeholder
     is not a per-objective form; there is exactly one, above the grid.
   - Each objective's `@foreach` over `objective.Tasks` (data already
     present via `GetPlannerForProjectAsync`'s existing `Include` chain —
     no query change needed there beyond `.AsSplitQuery()`) renders a
     `<TaskRow>` per task, replacing the placeholder once ≥1 task exists.
   - A new "Ungrouped" section, populated by
     `GetUngroupedTasksForProjectAsync`, rendered only when non-empty,
     using the same `<TaskRow>` component.

No entity, schema, or migration changes — `WorkItem`, its FKs, and
`PlanningRules.ValidateTaskTitle` already exist from `scaffold-blazor-solution`.

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── GetProjectsAsync()                        [existing]
├── AddProjectAsync(...)                      [existing]
├── GetProjectSummaryAsync(...)                [existing]
├── GetCurrentManagerIdAsync()                 [existing]
├── AddObjectiveAsync(...)                     [existing]
├── GetPlannerForProjectAsync(...)              [existing, + .AsSplitQuery()]
├── AddTaskAsync(...)                           [new]
├── GetTeamMembersAsync()                       [new]
└── GetUngroupedTasksForProjectAsync(...)       [new]

src/ManagerPlanner.Web/Components/Pages/
├── ProjectDetail.razor                         (extended)
│   ├── existing: summary counts + Refresh button
│   ├── existing: add-objective form + 3-column header
│   ├── new: unified "Add task" form (title, objective, assignee,
│   │        deadline, description, discovered checkbox)
│   ├── new: per-objective task rows via <TaskRow> (replaces placeholder)
│   └── new: "Ungrouped" section via <TaskRow> (shown only if non-empty)
└── TaskRow.razor                               (new)
    [Parameter] WorkItem Task
    renders: title + deadline | assignee-or-"Unassigned" + status | (empty)
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `AddTaskAsync(projectId, title, description, assigneeId, deadline, isDiscovered, objectiveId)`, + `GetTeamMembersAsync()`, + `GetUngroupedTasksForProjectAsync(projectId)`; `GetPlannerForProjectAsync` gains `.AsSplitQuery()` |
| `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` | Create | 3-column task-row component: `[Parameter] public WorkItem Task` |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | + unified "Add task" form; real per-objective rows via `<TaskRow>`; new "Ungrouped" section |

## Data Model Changes

None. This change reuses the entity model and schema from
`scaffold-blazor-solution` exactly as-is — no new fields, entities, or
migrations. `WorkItem.DiscoveredInMeetingId` remains in the schema,
unset by any code path this item adds.

## API Changes

None. No HTTP/JSON API — the page calls `PlanningService` directly, as
established in `project-management`/`planner-grid`.

## Key Decisions

1. **`AddTaskAsync` drops the `discoveredInMeetingId` parameter entirely**,
   rather than porting the full 8-parameter legacy signature and always
   passing `null`. Nothing in this item's UI can supply a meeting id (no
   `Meeting` entity/UI exists — rebuild-backlog item 6), so carrying the
   parameter would be a permanently-dead one, not a faithful port of
   *usable* surface area. Item 6 can add it back when it actually wires
   meeting discovery.
2. **`Description` is not trimmed at the service layer, but IS trimmed at
   the page layer** — matching the legacy call chain exactly, not just one
   link of it. `AddTaskAsync`'s service body stores `Description =
   description` verbatim (`../manager-planner/src/ExecutivePlanning.Core/
   Services/PlanningService.cs:104-105`, unlike `Title = title.Trim()` in
   the same method) — but the real legacy **caller**,
   `MainWindowViewModel.AddTaskAsync`, pre-processes it before the call:
   `string.IsNullOrWhiteSpace(NewTaskDescription) ? null :
   NewTaskDescription.Trim()` (`ViewModels/MainWindowViewModel.cs:146`).
   Reading only the service method (as the original proposal/spec draft
   did) would have shipped a page that preserves untrimmed whitespace —
   a real end-to-end fidelity miss caught by reading the full call chain,
   not just the method being ported. `ProjectDetail.razor`'s handler
   applies the same pre-processing the ViewModel does, before calling the
   service.
3. **`GetUngroupedTasksForProjectAsync` is a new, non-ported method** —
   Manager Planner Desktop's legacy grid never rendered a task with no
   objective (its only add-task path always supplied one), so there is no
   legacy query to port for this case. It exists only because the unified
   form (this item's own design, resolving the proposal's decision to drop
   the per-objective-only fast-add path) can now produce
   `ObjectiveId == null` tasks that still need to be visible somewhere.
4. **A `TaskRow.razor` component, not inline duplication.** The 3-column
   row markup is needed at two render sites in this same change
   (per-objective loop, Ungrouped section) — extracting it avoids literal
   duplication introduced right now, not speculative future reuse, and
   mirrors the legacy source's own named `TaskRowVm` row concept.
5. **Deadline displayed as UTC `yyyy-MM-dd`, not the legacy's local-time
   `MMM dd`.** This Blazor Server rebuild has not introduced any
   client-local-time concept so far (`GetProjectSummaryAsync`'s Overdue
   check already compares purely in UTC); porting the legacy's
   `.ToLocalTime()` display formatting now would introduce a timezone
   question ADR-0001 explicitly left open rather than resolve it. Kept
   simple and consistent with the rest of the app until a real decision
   is made.
6. **One unified "Add task" form, not one per objective.** Directly
   implements the proposal's decision 1 — Manager Planner Desktop's
   per-objective inline fast-add path (title only, `assigneeId: null`,
   hardcoded `+7 days` deadline) is not preserved as a second path
   alongside the full form.
7. **`.AsSplitQuery()` added to `GetPlannerForProjectAsync` now** — acts on
   `.specclaw/context.md`'s already-flagged follow-up ("Add
   `.AsSplitQuery()` once the child collections are non-trivial... when
   item 3+ starts populating real task rows"); this is that point.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — `WorkItem` entity fields and
  Business Rule 2 ("Task title required, ≤120 chars —
  `ValidateTaskTitle`"), already ported in `scaffold-blazor-solution`.
- `.specclaw/analysis/rebuild-backlog.md` item 3 — merge rationale (both
  legacy apps' create/view paths funnel into the same `AddTaskAsync`) and
  the two flagged forks this proposal already resolved.
- **Real legacy source**, read directly (not just doc summaries):
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:95-114` — exact `AddTaskAsync` shape,
    confirming `Title` is trimmed but `Description` is not (grounds
    FR1/Key Decision 2), and `GetTeamMembersAsync`'s exact filter/order
    (grounds FR2).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Desktop\
    Views\MainWindow.axaml:128-149` and `ViewModels\MainWindowViewModel.cs:
    138-156` — the real full "Add task" form's fields (title, assignee
    dropdown from `TeamMembers`, deadline `DatePicker`, optional
    description, "Discovered in a meeting" checkbox), confirming this is
    the form being unified to (FR5).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    ViewModels\MainViewModel.cs:120-131` — confirms the coarse inline
    fast-add path being dropped (`assigneeId: null`, hardcoded
    `DateTime.UtcNow.AddDays(7)`).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    ViewModels\RowViewModels.cs:36-94` (`TaskRowVm`) — the exact
    status-text `Humanize` mapping and owner-fallback logic (grounds
    FR6/Key Decision 4).
- `.specclaw/context.md` Key Patterns — "Add `.AsSplitQuery()` once the
  child collections are non-trivial... when item 3+ starts populating
  real task rows" (grounds Key Decision 7, directly actioned here) and the
  `IDbContextFactory` pattern (grounds NFR1).
- `.specclaw/adr/0001-target-platform-blazor-web.md` — "Multi-user/web
  concerns... become live questions" (grounds Key Decision 5's deferral of
  local-time display).

## Risks & Mitigations

- **Risk:** the assignee dropdown is untestable beyond "renders empty"
  since no `TeamMember` users are seeded yet. **Mitigation:** explicitly
  flagged in the proposal's Open Questions and accepted as this item's
  scope (AC6 tests the empty-render path, not a populated one); seeding
  stays rebuild-backlog item 11's job.
- **Risk:** introducing `GetUngroupedTasksForProjectAsync` as a second
  query alongside `GetPlannerForProjectAsync` could drift out of sync with
  it (e.g. a future change to one's `Include` shape not mirrored in the
  other). **Mitigation:** both are small, single-purpose reads with
  identical eager-load shape called out explicitly in FR3 — revisit if a
  third case ever needs the same shape a third time.
- **Risk:** the unified form silently changing what Manager Planner
  Desktop users could previously do quickly (one-field inline add) could
  read as a regression. **Mitigation:** explicitly an intentional,
  proposal-approved decision (decision 1), not an oversight — recorded
  here and in spec.md's Notes.
