# Proposal: Task (WorkItem) creation and viewing

**Created:** 2026-07-28
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

`WorkItem` (the "task") is, per domain-model.md, "the hub of the whole
accountability feature — every other tracking entity (`ProgressNote`,
`StatusChange`, `ChecklistItem`, `TaskOwner`) hangs directly off it." The
`planner-grid` change built the grid's static shell — an "Add objective"
form, the fixed 3-column header, and a "No tasks yet." placeholder per
objective — explicitly deferring all task-row content to this item.
Rebuild-backlog item 3 sequences here for exactly that reason: nothing in
items 4 (status/audit), 5 (checklist/badges), 7 (progress notes), or 8
(accountability) can be built or meaningfully tested until a `WorkItem` can
actually be created and seen.

Rebuild-backlog item 3 also merges two legacy affordances that differ only
in field completeness: Executive Planning Desktop's full "Add a task" form
(title, assignee dropdown, deadline, optional description, "Discovered in a
meeting" checkbox → the real `AddTaskAsync(projectId, title, description,
assigneeId, deadline, isDiscovered, discoveredInMeetingId, objectiveId)` in
`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:95`)
versus Manager Planner Desktop's coarse per-objective inline add (title
only — `MainViewModel.OnAddTask` always calls `AddTaskAsync` with
`assigneeId: null` and a hardcoded `DateTime.UtcNow.AddDays(7)` deadline,
confirmed at `../manager-planner/src/ManagerPlanner.Desktop/ViewModels/
MainViewModel.cs:120-131`). The rebuild-backlog flagged this as a fork
needing a human decision rather than a silent merge.

**Decisions supplied for this item** (resolving rebuild-backlog item 3's two
flagged forks, so this proposal targets one concrete design rather than
carrying the fork forward):

1. **Unify to one full create form.** The rebuild has exactly one task-
   creation surface — title, assignee, deadline, optional description (plus
   the "Discovered in a meeting" checkbox, decision 2) — matching Executive
   Planning Desktop's full form. Manager Planner Desktop's coarse inline
   fast-add path (title-only, null assignee, hardcoded +7-day deadline) is
   **not** preserved as a separate path.
2. **`DiscoveredInMeetingId` stays dormant.** The checkbox sets
   `IsDiscovered = true` only, exactly matching both legacy apps' actual
   behavior today (functional-spec.md Named Gap #2: "never set through
   either app's UI"). No meeting-selection UI is wired in this item —
   `Meeting` itself doesn't exist yet (rebuild-backlog item 6, not built).

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains two methods**, ported from the real legacy
   `Services/PlanningService.cs`:
   - `AddTaskAsync(projectId, title, description, assigneeId, deadline,
     isDiscovered, objectiveId)` — validates `title` via the already-ported
     `PlanningRules.ValidateTaskTitle` (≤120 chars), trims it, and persists a
     `WorkItem`. **No `discoveredInMeetingId` parameter** — per decision 2,
     nothing in this item can supply one (no `Meeting` UI exists), so it's
     left off the signature entirely rather than threading a permanently-null
     parameter through; item 6 can extend this method when it actually wires
     meeting discovery.
   - `GetTeamMembersAsync()` — ported exactly from legacy
     (`_db.Users.Where(u => u.Role == UserRole.TeamMember && u.IsActive)
     .OrderBy(u => u.FullName)`), needed to populate the assignee dropdown.
     No such method exists in `ManagerPlanner.Core` yet — only
     `GetCurrentManagerIdAsync` reads `Users` today.

2. **Extend the existing Planner Grid section on `/projects/{id}`** (built by
   `planner-grid`) rather than adding a new page or route:
   - **One "Add task" form**, not per-objective: Title (required), an
     Objective dropdown (optional — see Open Questions), an Assignee
     dropdown sourced from `GetTeamMembersAsync` (optional — a task can be
     unassigned, matching `WorkItem.AssigneeId`'s nullability), a Deadline
     date input (optional, matching `WorkItem.Deadline`'s nullability — the
     legacy full form never requires one), an optional Description, and the
     "Discovered in a meeting" checkbox. Same inline-error pattern as the
     existing add-objective/add-project forms: a thrown `ValidationException`
     shows its `.Message` inline, no `WorkItem` is created.
   - **Real task rows**, replacing each objective's "No tasks yet."
     placeholder: a 3-column row per task — Task cell (Title, plus deadline
     text if set); Owner/status cell (`Assignee.FullName` or "Unassigned",
     plus the task's status text); Checklist cell (empty placeholder — item 5
     hasn't built checklist rendering yet). **No OVERDUE/discovered badges**
     (item 5's job) and **no status-change controls** (item 4's job, not
     built) — the status text is always "Not started" for now since nothing
     can change it yet.
   - **An "Ungrouped tasks" section** for tasks with `ObjectiveId == null` —
     see Open Questions; this case doesn't exist in Manager Planner Desktop's
     legacy grid (its only add-task path always supplied an objective) but
     is now reachable once the unified form allows creating a task with no
     objective, mirroring Executive Planning Desktop's tasks (which have no
     `Objective` concept at all).

## Scope

### In Scope
- `PlanningService.AddTaskAsync` (7-parameter form above, no
  `discoveredInMeetingId`) and `PlanningService.GetTeamMembersAsync`
- One unified "Add task" form on `/projects/{id}`'s Planner Grid section:
  title, objective (optional), assignee (optional), deadline (optional),
  description (optional), "Discovered in a meeting" checkbox
- Real task-row rendering in the Planner Grid (Title, deadline text,
  assignee-or-"Unassigned", status text, empty checklist placeholder)
- An "Ungrouped tasks" section for `ObjectiveId == null` tasks
- `WorkItem` validation via the already-existing `PlanningRules.ValidateTaskTitle`

### Out of Scope
- **Manager Planner Desktop's coarse per-objective inline fast-add path** —
  explicitly dropped per decision 1, not preserved alongside the unified form
- **`DiscoveredInMeetingId` wiring / a meeting-link dropdown** — dormant per
  decision 2; arrives with rebuild-backlog item 6 (Meeting)
- **OVERDUE / "⚑ discovered" visual badges and the nested checklist tree** —
  rebuild-backlog item 5
- **Status-change buttons/commands** — rebuild-backlog item 4; status always
  reads "Not started" in this item
- **`TaskOwner` (many-to-many "owners") assignment** — functional-spec.md
  Named Gap #4 confirms no legacy UI sets this; still unresolved, not this
  item's job to fix
- **Click-through task selection / a task detail or notes page** — both
  legacy apps' "select a task" behavior exists only to drive a notes list or
  Task+Notes window, which is rebuild-backlog item 7's job (no `ProgressNote`
  UI exists yet)
- **Task deletion** — rebuild-backlog item 9

## Impact

- **Files affected:** ~2 (estimated) — `PlanningService.cs` (2 new methods,
  no new file), `ProjectDetail.razor` (extended: add-task form, real task
  rows, ungrouped-tasks section)
- **Complexity:** small–medium — mechanical port of `AddTaskAsync`/
  `GetTeamMembersAsync` plus meaningfully more UI surface than
  `planner-grid`'s static shell (a multi-field form, real row data, a new
  ungrouped bucket)
- **Risk:** low — grounded directly against the real legacy source
  (`C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
  Services\PlanningService.cs` and both desktop apps' ViewModels/AXAML), not
  just doc summaries; both flagged forks were resolved by explicit decision
  before this proposal was drafted

## Open Questions

1. **Include an optional "Objective" dropdown in the unified form?**
   Recommended: yes. It wasn't named in the field list you supplied, but
   it's structurally necessary — `planner-grid` already groups the grid by
   objective, and `WorkItem.ObjectiveId` is nullable, so the single form
   needs a way to place a task in a group or leave it ungrouped. Say so if
   you'd rather the form always require an objective (removing the
   "ungrouped tasks" case), or always omit one (forcing every task
   ungrouped, which would make objectives pointless once this item ships).
2. **Deadline: optional, matching legacy?** Recommended: yes — leave it
   optional. `WorkItem.Deadline` is nullable and Executive Planning
   Desktop's real full form never enforces one (only the *dropped* fast-add
   path hardcoded a deadline, as a side effect of its coarseness, not a
   validated rule).
3. **"Ungrouped tasks" section — new, since the legacy Manager Planner grid
   never produced this case.** Recommended: a simple labeled section (e.g.
   "Ungrouped") rendered alongside the per-objective sections, using the
   same 3-column row layout. Flag if you'd rather force every task to have
   an objective instead (removing this section and the corresponding form
   choice in Q1).
4. **Assignee dropdown will start empty.** `project-management`'s startup
   bootstrap only guarantees one `Role = Manager` user — no `TeamMember`
   rows exist until rebuild-backlog item 11 (sample-data seeding) runs.
   Recommended: leave it empty for now (assignee is optional, so this
   doesn't block task creation) rather than adding ad hoc team-member
   bootstrap data here, keeping seeding entirely item 11's job. Flag if
   you'd rather seed a couple of team members now for a testable dropdown.

---

**To proceed:** Review this proposal and approve to begin planning.
