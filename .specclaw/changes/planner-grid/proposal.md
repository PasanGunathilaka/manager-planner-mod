# Proposal: Objective grouping and the planner grid

**Created:** 2026-07-27
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

`Objective` sits between `Project` and `WorkItem` in the domain hierarchy —
"Sits between Project and WorkItem so work is grouped the way a manager
plans it: Project → Objective → Task" (domain-model.md). Rebuild-backlog
item 2 sequences it right after Project management (item 1) specifically
because Manager Planner Desktop's *only* task-creation affordance is the
Planner Grid's per-objective inline form — so an Objective must exist
before item 3 (Task/WorkItem) can be built and used, even though the FK
itself (`WorkItem.ObjectiveId`) is nullable.

Rebuild-backlog item 2 merges "add an objective" and "view the grid it
populates" into one feature, since they're inseparable in the legacy
source (`PlannerGridView.axaml`). Reading that source directly
(`C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\Views\PlannerGridView.axaml`)
confirms the grid is one always-visible layout: an "Add objective" bar, a
fixed 3-column header ("Tasks" | "Owner / status" | "Progress checklist"),
then each Objective as a heading followed by its tasks. Task-row content
(owners, status, checklist, the inline "add task" form) belongs to items
3–5, which haven't been built yet — this item builds the objective layer
and the grid shell those items will populate.

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains two methods**, ported from the real legacy
   `Services/PlanningService.cs`:
   - `AddObjectiveAsync(projectId, title, keyResult = null)` — validates
     `title` via the already-ported `PlanningRules.ValidateObjectiveTitle`
     (≤150 chars), trims it, and sets `SortOrder` to the current count of
     objectives for that project (append-only — matches legacy exactly:
     `var order = await _db.Objectives.Where(o => o.ProjectId ==
     projectId).CountAsync();`).
   - `GetPlannerForProjectAsync(projectId)` — objectives ordered by
     `SortOrder`, with the **exact same eager-load chain the legacy code
     uses** (`.Include(o => o.Tasks).ThenInclude(t => t.Assignee)`,
     `...ThenInclude(t => t.Owners).ThenInclude(w => w.User)`,
     `...ThenInclude(t => t.Checklist)`) even though every `Objective.Tasks`
     collection will be empty until item 3 exists — this is the same
     method items 3–5 will read from once they add task-row rendering, so
     porting its full shape now avoids a second, divergent query later.

2. **Extend `/projects/{id}`** (built in `project-management`, which
   already earmarked this page as "the shell later items attach their own
   sections to") with a **Planner Grid section**:
   - An inline "Add objective" form: a single Title text field + "Add"
     button, matching the legacy bar exactly (no Key Result input — see
     Open Questions).
   - The fixed 3-column header row ("Tasks" | "Owner / status" |
     "Progress checklist"), matching the legacy layout.
   - Each Objective rendered as a heading (Title), followed by a "No tasks
     yet." placeholder in place of task rows — since no `WorkItem` can
     exist until item 3 ships.

## Scope

### In Scope
- `PlanningService.AddObjectiveAsync` and `GetPlannerForProjectAsync`
- A "Planner Grid" section added to `/projects/{id}`: Add-objective form +
  3-column header + one heading per objective + "No tasks yet." placeholder
- `Objective` validation via the already-existing `PlanningRules.ValidateObjectiveTitle`

### Out of Scope
- **A Key Result input field.** Reading the legacy source directly
  confirms the running app has no such field — `AddObjectiveCommand` calls
  `_service.AddObjectiveAsync(SelectedProject.Id, NewObjectiveTitle)`,
  always passing `keyResult: null` by omission. `Objective.KeyResult`
  exists in the schema but no legacy UI ever sets it — an inherited gap,
  not something to silently fix here (see Open Questions).
- **Task-row rendering** — owners, status text, the checklist tree,
  OVERDUE/discovered badges — these attach to the same grid in items 3
  (Task/WorkItem), 4 (status), and 5 (checklist).
- **The inline "add task to this objective" form** — item 3's job; the
  legacy `AddTaskCommand` lives in the same view but is a distinct
  capability per rebuild-backlog's own item split.
- **Objective deletion, editing, or reordering** — no legacy UI exists for
  any of these; `SortOrder` is fixed at creation time, append-only.

## Impact

- **Files affected:** ~2 (estimated) — `PlanningService.cs` (2 new
  methods, no new file), `ProjectDetail.razor` (extended with the new
  section)
- **Complexity:** small — mechanical port of two query methods plus a
  static-shell UI section with no interactive task data yet
- **Risk:** low — grounded directly against the real legacy source
  (`C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\Views\PlannerGridView.axaml`
  and `ViewModels\MainViewModel.cs`), not just doc summaries

## Open Questions

1. **Omit the Key Result input, matching legacy exactly?** Recommended:
   yes — the legacy app never exposes it despite the schema supporting it;
   preserving that gap keeps behavior faithful. If you'd rather add a
   Key Result field now (a small, easy addition, since the entity already
   has the column and no migration is needed), say so and I'll fold it in
   at the design stage.
2. **Build the grid's static shell (headers + empty per-objective
   sections) now, ahead of item 3's task data existing?** Recommended:
   yes — this is exactly what rebuild-backlog's merge rationale calls
   for ("adding an objective and viewing the grid it immediately
   populates are inseparable"), and avoids item 3 having to also invent
   the grid layout.
3. **Confirm the Planner Grid lives as a new section on the existing
   `/projects/{id}` page**, not a separate route — this matches what
   `project-management`'s design.md already flagged as the intended home
   for this item.

---

**To proceed:** Review this proposal and approve to begin planning.
