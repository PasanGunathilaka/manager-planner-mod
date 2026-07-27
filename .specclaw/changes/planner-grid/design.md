# Design: Objective grouping and the planner grid

**Change:** planner-grid
**Created:** 2026-07-27

## Technical Approach

1. Add two methods to the existing `ManagerPlanner.Core/Services/PlanningService.cs`
   (no new file), ported directly from the real legacy
   `Services/PlanningService.cs`: `AddObjectiveAsync` and
   `GetPlannerForProjectAsync`. Both follow the established
   `IDbContextFactory<PlanningDbContext>` pattern (open/dispose a
   short-lived context per call) already used by the four existing methods.
2. Extend the existing `ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`
   (no new file) with a new "Planner Grid" section beneath the summary:
   an add-objective form (modeled directly on `Projects.razor`'s
   create-project form — `EditForm` + `InputText` + `ValidationException`
   catch + in-place list refresh), a fixed 3-column header, and one
   heading + "No tasks yet." placeholder per objective.

No new files, no new routes, no DI changes, no entity/schema/migration
changes — this is the smallest possible slice that satisfies
rebuild-backlog item 2's merge rationale.

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── GetProjectsAsync()                  [existing]
├── AddProjectAsync(...)                [existing]
├── GetProjectSummaryAsync(...)         [existing]
├── GetCurrentManagerIdAsync()          [existing]
├── AddObjectiveAsync(...)              [new]
└── GetPlannerForProjectAsync(...)      [new]

src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor  (extended)
├── existing: summary counts + Refresh button
└── new: Planner Grid section
    ├── add-objective form (Title + Add button)
    ├── 3-column header ("Tasks" | "Owner / status" | "Progress checklist")
    └── per-objective: heading (Title) + "No tasks yet." placeholder
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `AddObjectiveAsync(projectId, title, keyResult = null)`, + `GetPlannerForProjectAsync(projectId)` |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | + Planner Grid section: add-objective form, 3-column header, per-objective heading + placeholder |

## Data Model Changes

None. `Objective`, `WorkItem`, and their relationships already exist from
`scaffold-blazor-solution`'s migration — no new fields, entities, or
migrations.

## API Changes

None. No HTTP/JSON API — the page calls `PlanningService` directly, as
established in prior changes.

## Key Decisions

1. **Port `GetPlannerForProjectAsync`'s full `Include` chain now**
   (`Tasks`→`Assignee`, `Tasks`→`Owners`→`User`, `Tasks`→`Checklist`),
   even though every `Objective.Tasks` collection is empty until item 3 —
   this is the exact method the legacy grid binds to
   ("`GetPlannerForProjectAsync` ... This is what the grid window binds
   to," legacy `PlanningService.cs`), and porting its full shape now means
   items 3–5 extend one method's *rendering*, not its *query*.
2. **No Key Result input field** — confirmed by reading the real legacy
   `MainViewModel.cs` directly: `AddObjectiveCommand` calls
   `_service.AddObjectiveAsync(SelectedProject.Id, NewObjectiveTitle)`,
   never passing a `keyResult` argument. `Objective.KeyResult` stays in
   the entity/schema but has no UI path to set it — an inherited legacy
   gap, preserved rather than "fixed."
3. **Build the static grid shell now, ahead of item 3's data** — directly
   actions rebuild-backlog item 2's own merge rationale ("adding an
   objective and viewing the grid it immediately populates are
   inseparable"). The "No tasks yet." placeholder is an intentional,
   temporary state for this change, not a defect.
4. **No new Razor component extracted** — the addition (a form, a fixed
   header, a loop over objectives with a placeholder) is small enough to
   stay inline in `ProjectDetail.razor`, matching the file's existing
   style. Revisit component extraction once items 3–5 add real task-row
   complexity to this same section — premature extraction now would be
   speculative structure for content that doesn't exist yet.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — `Objective` entity fields and
  Business Rule 3 ("Objective title required, ≤150 chars —
  `ValidateObjectiveTitle`"), already ported in `scaffold-blazor-solution`.
- `.specclaw/analysis/rebuild-backlog.md` item 2 — merge rationale
  ("adding an objective and viewing the grid it immediately populates are
  inseparable in the source") and dependency ordering (depends on item 1;
  blocks item 3).
- **Real legacy source**, read directly (not just doc summaries):
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs`
    — exact `AddObjectiveAsync`/`GetPlannerForProjectAsync` shapes
    (grounds FR1/FR2 and Key Decision 1).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\Views\PlannerGridView.axaml`
    — the add-objective bar, 3-column header layout, and per-objective
    grouping structure (grounds FR3/FR4/FR5).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\ViewModels\MainViewModel.cs`
    — confirms `AddObjectiveCommand` never passes a `keyResult` argument
    (grounds NFR3/Key Decision 2).
- `.specclaw/context.md` — the `IDbContextFactory` pattern and
  "ground-truth against the real legacy source" practice, both directly
  applied here.

## Risks & Mitigations

- **Risk:** shipping UI for a feature with no populated data yet (empty
  task rows) could read as broken to an end user. **Mitigation:** the
  "No tasks yet." placeholder makes the temporary state explicit; this
  was an explicit proposal open question the user signed off on before
  planning.
- **Risk:** `SortOrder`'s count-based assignment isn't safe under
  concurrent adds to the same project. **Mitigation:** none added — this
  matches the legacy service's own behavior exactly (no
  transaction/locking there either); introducing new concurrency
  safeguards here would be scope creep beyond a faithful port.
- **Risk:** the new add-objective form diverging in structure/behavior
  from `Projects.razor`'s existing create-project form, creating
  inconsistent UX. **Mitigation:** explicitly modeled on that existing
  form (same `EditForm`/`InputText`/`ValidationException`-catch/in-place-
  refresh pattern) rather than designed independently.
