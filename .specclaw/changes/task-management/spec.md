# Spec: Task (WorkItem) creation and viewing

**Change:** task-management
**Created:** 2026-07-28
**Status:** 🟡 Draft

## Overview

Adds `WorkItem` (task) creation and grid-row viewing to the rebuild: three
new `PlanningService` methods (two ported from the real legacy source, one
new with no legacy equivalent), and an extension of the existing "Planner
Grid" section on `/projects/{id}` (built by `planner-grid`) — one unified
"Add task" form replacing both legacy apps' create paths, plus real task
rows in place of every "No tasks yet." placeholder. Status-change controls,
checklist rendering, OVERDUE/discovered badges, `TaskOwner` assignment, and
task-detail/notes navigation are explicitly deferred to later backlog items
(4, 5, 7, 9) and do not appear in this change.

## Requirements

### Functional Requirements

1. **FR1 — `AddTaskAsync(projectId, title, description, assigneeId,
   deadline, isDiscovered, objectiveId)`.** Ported from the real legacy
   `AddTaskAsync` (`../manager-planner/src/ExecutivePlanning.Core/Services/
   PlanningService.cs:95-114`), with **no `discoveredInMeetingId`
   parameter** (dropped per proposal decision 2 — nothing in this item can
   supply one). Validates `title` via the already-ported
   `PlanningRules.ValidateTaskTitle` (≤120 chars). **At the service layer,
   `description` is stored exactly as passed, not trimmed** — matches the
   legacy service's `Description = description` verbatim (no `.Trim()`
   there, unlike `title`). The full end-to-end fidelity target is the
   legacy **caller**, though: `MainWindowViewModel.AddTaskAsync`
   pre-processes it before calling the service —
   `string.IsNullOrWhiteSpace(NewTaskDescription) ? null :
   NewTaskDescription.Trim()` (`../manager-planner/src/
   ExecutivePlanning.Desktop/ViewModels/MainWindowViewModel.cs:146`) — so
   the Blazor page (FR5) applies that same pre-processing before calling
   `AddTaskAsync`, matching the legacy app's actual observed behavior
   rather than the service method in isolation. `Status`/`CreatedUtc` come
   from `WorkItem`'s own entity defaults (`NotStarted` / `DateTime.UtcNow`),
   not set at the call site — matching every prior entity-creation method
   in this codebase.
2. **FR2 — `GetTeamMembersAsync()`.** Ported exactly from legacy
   (`_db.Users.Where(u => u.Role == UserRole.TeamMember && u.IsActive)
   .OrderBy(u => u.FullName)`), used to populate the assignee dropdown.
3. **FR3 — `GetUngroupedTasksForProjectAsync(projectId)`.** New — **no
   legacy equivalent**, needed because the unified form (FR5) can produce a
   task with `ObjectiveId == null`, a case Manager Planner Desktop's legacy
   grid never rendered (its only add-task path always supplied an
   objective). Returns `db.WorkItems.Where(t => t.ProjectId == projectId &&
   t.ObjectiveId == null)` with the same eager-load shape as
   `GetPlannerForProjectAsync`'s task Includes (`Assignee`,
   `Owners.User`, `Checklist`).
4. **FR4 — `GetPlannerForProjectAsync` gains `.AsSplitQuery()`.** Its
   `Tasks`→`Owners` + `Tasks`→`Checklist` `Include` chain already logs EF
   Core's cartesian-product warning (harmless while `Tasks` was empty,
   per `.specclaw/context.md`'s Key Patterns); this item is the point that
   flag said to act on, since real task rows now populate that collection.
5. **FR5 — One unified "Add task" form on `/projects/{id}`**, not
   per-objective: Title (required), an Objective `<select>` (optional,
   default "— Ungrouped —", options from the already-loaded objective
   list), an Assignee `<select>` (optional, default "— Unassigned —",
   options from `GetTeamMembersAsync`), a Deadline date input (optional),
   a Description textarea (optional), and a "Discovered in a meeting"
   checkbox (sets `IsDiscovered` only — no meeting-link control exists).
   On a thrown `ValidationException`, its `.Message` shows inline and no
   `WorkItem` is created (same pattern as the existing add-objective/
   add-project forms). On success, the form clears and the grid refreshes
   in place — no full page reload.
6. **FR6 — Real per-objective task rows.** Each objective's tasks (already
   returned by `GetPlannerForProjectAsync`'s existing `Include` chain — no
   query change needed there beyond FR4) render as rows in place of the
   "No tasks yet." placeholder, replacing that placeholder only once the
   objective has ≥1 task. Each row shows: Title, deadline (formatted
   `yyyy-MM-dd`, UTC, or nothing if unset — see Notes on why this diverges
   from the legacy's local-time "MMM dd" format); Assignee's `FullName` or
   "Unassigned"; status text via the same NotStarted→"Not started" /
   InProgress→"In progress" / Blocked→"Blocked" / Done→"Done" mapping the
   legacy `TaskRowVm.Humanize` uses (`RowViewModels.cs:87-94`) — always
   "Not started" today since nothing can change it yet, but the mapping
   itself is forward-compatible with item 4. **No OVERDUE/discovered
   badges, no checklist content, no status-change controls** — items 4/5.
7. **FR7 — An "Ungrouped" section**, rendered only when the project has
   ≥1 task with `ObjectiveId == null` (via FR3), using the identical row
   rendering as FR6, positioned after all per-objective sections.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** All three new `PlanningService` methods
   use `IDbContextFactory<PlanningDbContext>` (already the constructor's
   type), consistent with every existing method — no direct
   `PlanningDbContext` injection.
2. **NFR2 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: OVERDUE/"⚑ discovered" badges or the nested
   checklist tree (item 5); status-change buttons/commands (item 4);
   `TaskOwner` (many-to-many "owners") assignment UI (functional-spec.md
   Named Gap #4 — still unresolved, not this item's job); click-through
   task selection or a task detail/notes page (item 7); task deletion
   (item 9); a `discoveredInMeetingId` parameter anywhere on
   `AddTaskAsync` or a meeting-link control in the form (item 6).
3. **NFR3 — Row template reuse.** The 3-column task-row markup (task
   cell, owner/status cell, checklist-placeholder cell) is defined once
   and used by both the per-objective loop (FR6) and the Ungrouped section
   (FR7) — not duplicated inline in two places.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Submitting the full form (title, objective, assignee,
   deadline, description, discovered checkbox checked) persists a
   `WorkItem` with the correct `ProjectId`, trimmed `Title`, a trimmed
   `Description` (matching the legacy caller's pre-processing — see FR1),
   the chosen `ObjectiveId`/`AssigneeId`/`Deadline`, `IsDiscovered ==
   true`, and `DiscoveredInMeetingId == null` — and the grid updates in
   place without a full page reload.
3. **AC3** — Submitting with only Title filled in (objective, assignee,
   deadline, description all left at their defaults, checkbox unchecked)
   persists a `WorkItem` with `ObjectiveId == null`, `AssigneeId == null`,
   `Deadline == null`, `Description == null`, `IsDiscovered == false`, and
   it appears in the "Ungrouped" section (AC7 confirms zero-state
   behavior when none exist yet).
4. **AC4** — Submitting an empty/whitespace-only title, or a title over
   120 characters, shows the `PlanningRules` validation message inline
   and creates no `WorkItem` row; all other field values entered are not
   silently discarded from the visible form (standard form-preserves-input-
   on-error behavior — no explicit legacy behavior to match here since the
   legacy apps never round-trip a failed submit visibly beyond a message
   dialog).
5. **AC5** — A task submitted with an `Objective` selected appears within
   that objective's section in the grid, and that objective's "No tasks
   yet." placeholder no longer renders for it.
6. **AC6** — The assignee `<select>` is populated from
   `GetTeamMembersAsync` (Role == TeamMember, IsActive, ordered by
   FullName); with zero team members seeded (the current state — only a
   bootstrapped Manager exists), it renders with only the "— Unassigned —"
   default option and does not error.
7. **AC7** — The "Ungrouped" section renders only when the project has at
   least one task with `ObjectiveId == null`; a project with zero
   ungrouped tasks shows no "Ungrouped" heading at all (not an empty one).
8. **AC8** — Each rendered task row shows Title, deadline (or nothing if
   unset), assignee-or-"Unassigned", and status text ("Not started" for
   every task, since nothing built so far can change it) — and no
   OVERDUE/discovered badge, no checklist markup, and no status-change
   button/command exists anywhere in the rendered output for any row.
9. **AC9** — No `PlanningService` method beyond the six existing ones
   (`GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`,
   `GetCurrentManagerIdAsync`, `AddObjectiveAsync`,
   `GetPlannerForProjectAsync`) plus the three added here (`AddTaskAsync`,
   `GetTeamMembersAsync`, `GetUngroupedTasksForProjectAsync`) exists
   anywhere in the diff.

## Edge Cases

- **Zero team members seeded.** Assignee dropdown shows only "—
  Unassigned —"; task creation without an assignee still succeeds (AC6).
- **Task created with no objective.** Renders in the "Ungrouped" section,
  not silently dropped (AC3, AC7).
- **Overlong or arbitrarily long description.** No length limit exists —
  `PlanningRules` has no `ValidateDescription` rule, matching the legacy
  domain model exactly (only `Title` is validated on `WorkItem`); do not
  invent a ceiling here.
- **Deadline in the past.** Allowed with no validation error — the legacy
  domain has no future-only rule for `WorkItem.Deadline` (unlike
  `ProgressNote.NoteDate`'s backdate/future window); it will simply have no
  visible "OVERDUE" effect yet since item 5 doesn't exist.
- **Ordering of multiple tasks within the same objective or the Ungrouped
  section.** Unspecified — `WorkItem` has no `SortOrder` field (unlike
  `Objective`/`ChecklistItem`), matching the legacy schema exactly; render
  in whatever order the query returns them, do not invent a sort key.
- **Duplicate task titles, within an objective or across the project.**
  Allowed — no uniqueness rule exists in `PlanningRules.ValidateTaskTitle`
  or anywhere in the domain model; do not invent one.

## Dependencies

- **Depends on:** `scaffold-blazor-solution` (entities/rules),
  `project-management` (service/page conventions, `GetCurrentManagerIdAsync`
  pattern), `planner-grid` (`/projects/{id}`'s Planner Grid section,
  `GetPlannerForProjectAsync`, the per-objective sections this item
  populates with real rows).
- **Blocks:** item 4 (status transitions — needs a `WorkItem` to exist),
  item 5 (checklist/badges — needs real task rows to attach to), item 7
  (progress notes — needs a `WorkItem` to note against), item 9 (task
  deletion).

## Notes

Four proposal open questions are resolved by proceeding to this spec: (1)
the unified form includes an optional Objective dropdown; (2) Deadline is
optional, matching the legacy full form's actual (unenforced) behavior;
(3) an "Ungrouped" section is added, shown only when populated (FR7/AC7);
(4) the assignee dropdown is left to start empty — no ad hoc team-member
bootstrap is added in this item, keeping seeding entirely
rebuild-backlog item 11's job.

One new formatting decision, not flagged in the proposal: deadline display
uses UTC `yyyy-MM-dd`, not the legacy `TaskRowVm`'s
`.ToLocalTime().ToString("MMM dd")`. Nothing else in this Blazor Server
rebuild has introduced a client-local-time concept yet (`GetProjectSummaryAsync`'s
Overdue check already compares against `DateTime.UtcNow`, never local
time) — porting a local-time display format now would introduce a
timezone question ADR-0001 explicitly deferred ("Multi-user/web concerns
absent from the legacy single-user desktop app... become live questions")
rather than resolve it. Revisit if/when a real timezone-handling decision
is made.
