# Baseline Scenarios: Manager Planner / Executive Planning

**Date generated:** 2026-07-30
**Grounded in:** .specclaw/analysis/domain-model.md's numbered Business Rules, cross-referenced
against codebase-report.md, architecture.md, functional-spec.md, rebuild-backlog.md,
clarifications.md, and direct reads of the legacy source at `../manager-planner` (see seams.md's
header note on repo layout).

## Scenarios

### GM-001 — Project name: empty rejected, exact 120-char boundary

- **Seam:** Pure function — `PlanningRules.ValidateProjectName` (`Services/PlanningValidation.cs:21-27`)
- **Business rules pinned:** DR-001
- **Arrange:** Three candidate strings: `"   "` (whitespace-only), a 120-char string, a 121-char string.
- **Act:** Call `ValidateProjectName` with each candidate.
- **Assert (shape):** Whitespace-only throws `ValidationException` with message
  `"Project name cannot be empty."`; the 120-char string does not throw; the 121-char string
  throws with message `"Project name is too long (121). Keep it to 120 characters."` (exact text
  read directly from source, resolving part of CQ-022 for this field).
- **Kind:** boundary
- **Verifies backlog item:** BL-001 (Project management)

### GM-002 — Task title: empty rejected, exact 120-char boundary

- **Seam:** Pure function — `PlanningRules.ValidateTaskTitle` (`:29-35`)
- **Business rules pinned:** DR-002
- **Arrange:** `"   "`, a 120-char string, a 121-char string.
- **Act:** Call `ValidateTaskTitle` with each.
- **Assert (shape):** Whitespace-only throws `"Task title cannot be empty."`; 120 chars accepted;
  121 chars throws `"Task title is too long (121). Keep it to 120 characters."`.
- **Kind:** boundary
- **Verifies backlog item:** BL-003 (Task/WorkItem creation and viewing)

### GM-003 — Objective title: empty rejected, exact 150-char boundary

- **Seam:** Pure function — `PlanningRules.ValidateObjectiveTitle` (`:37-43`)
- **Business rules pinned:** DR-003
- **Arrange:** `"   "`, a 150-char string, a 151-char string.
- **Act:** Call `ValidateObjectiveTitle` with each.
- **Assert (shape):** Whitespace-only throws `"Objective title cannot be empty."`; 150 chars
  accepted; 151 chars throws `"Objective title is too long (151). Keep it to 150 characters."`.
- **Kind:** boundary
- **Verifies backlog item:** BL-002 (Objective grouping and the planner grid)

### GM-004 — Checklist label: empty rejected, exact 300-char boundary

- **Seam:** Pure function — `PlanningRules.ValidateChecklistLabel` (`:45-51`)
- **Business rules pinned:** DR-004
- **Arrange:** `"   "`, a 300-char string, a 301-char string.
- **Act:** Call `ValidateChecklistLabel` with each.
- **Assert (shape):** Whitespace-only throws `"Checklist item cannot be empty."`; 300 chars
  accepted; 301 chars throws `"Checklist item is too long. Keep it to 300 characters."` (note:
  unlike the other four validators, this message does **not** interpolate the actual length —
  read directly from source, not an assumption).
- **Kind:** boundary
- **Verifies backlog item:** BL-005 (Nested checklist items and grid status badges)

### GM-005 — Note text: empty rejected, exact 2000-char boundary

- **Seam:** Pure function — `PlanningRules.ValidateNoteText` (`:53-59`)
- **Business rules pinned:** DR-005
- **Arrange:** `"   "`, a 2000-char string, a 2001-char string.
- **Act:** Call `ValidateNoteText` with each.
- **Assert (shape):** Whitespace-only throws `"The note is empty — type what was said before
  saving."` (already quoted in domain-model.md); 2000 chars accepted; 2001 chars throws `"The
  note is too long. Keep it under 2000 characters."` (this exact string resolves the remaining
  half of CQ-022 that domain-model.md left unquoted).
- **Kind:** boundary
- **Verifies backlog item:** BL-007 (Progress notes and promise tracking)

### GM-006 — Note date: exact one-month-back boundary, with pinned "now"

- **Seam:** Pure function — `PlanningRules.ValidateNoteDate(DateTime, DateTime? nowUtc)` (`:65-75`)
- **Business rules pinned:** DR-006
- **Arrange:** Fix `nowUtc` to a concrete anchor, e.g. `2026-07-30T00:00:00Z` (any fixed value
  works since the method is pure once `nowUtc` is supplied). `earliest = nowUtc.AddMonths(-1)` =
  `2026-06-30`. Two candidate note dates: `earliest` itself, and `earliest.AddDays(-1)` (one day
  earlier).
- **Act:** Call `ValidateNoteDate(candidate, nowUtc)` for each candidate.
- **Assert (shape):** `earliest` does not throw; `earliest.AddDays(-1)` throws
  `ValidationException` with message `"That date is more than a month back. Notes can only be
  dated on or after Jun 30, 2026."` (exact `MMM dd, yyyy` format read directly from source line 72).
- **Kind:** boundary
- **Verifies backlog item:** BL-007 (Progress notes and promise tracking)

### GM-007 — Note date: today accepted, tomorrow rejected

- **Seam:** Pure function — `PlanningRules.ValidateNoteDate` (`:65-75`)
- **Business rules pinned:** DR-006
- **Arrange:** Same fixed `nowUtc` anchor as GM-006. Candidates: `nowUtc` itself, and
  `nowUtc.AddDays(1)`.
- **Act:** Call `ValidateNoteDate(candidate, nowUtc)` for each.
- **Assert (shape):** `nowUtc` (today) does not throw; `nowUtc.AddDays(1)` throws
  `"A note cannot be dated in the future."`.
- **Kind:** edge case
- **Verifies backlog item:** BL-007 (Progress notes and promise tracking)

### GM-008 — Verdict precedence: PromiseKept wins outright

- **Seam:** Pure function — `AccountabilityRow.Verdict` (`Services/Reports.cs:37-47`), no DB
- **Business rules pinned:** DR-007
- **Arrange:** Construct an `AccountabilityRow` directly with `PromiseKept = true`,
  `PromiseBroken = true`, `IsOverdue = true`, `LatestPromisedDate = <any date>` (deliberately
  setting the "losing" flags true too, to prove precedence order, not just that Kept alone works).
- **Act:** Read `.Verdict`.
- **Assert (shape):** Equals exactly `"Kept promise"`.
- **Kind:** boundary
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-009 — Verdict precedence: PromiseBroken beats IsOverdue

- **Seam:** Pure function — `AccountabilityRow.Verdict`
- **Business rules pinned:** DR-007
- **Arrange:** `PromiseKept = false`, `PromiseBroken = true`, `IsOverdue = true`,
  `LatestPromisedDate = <any date>`.
- **Act:** Read `.Verdict`.
- **Assert (shape):** Equals exactly `"BROKE promise"`.
- **Kind:** boundary
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-010 — Verdict precedence: the CQ-019 mislabeling nuance ("Overdue (no promise)" despite a promise on record)

- **Seam:** Pure function — `AccountabilityRow.Verdict`
- **Business rules pinned:** DR-007 — this is the exact nuance domain-model.md and
  rebuild-backlog.md (CQ-019/CQ-024) flag as the single highest-priority verification item in the
  whole backlog.
- **Arrange:** `PromiseKept = false`, `PromiseBroken = false`, `IsOverdue = true`,
  `LatestPromisedDate = <a real, non-null future date>` — i.e. a promise genuinely exists and is
  not yet due/broken, but the task's own deadline has separately passed.
- **Act:** Read `.Verdict`.
- **Assert (shape):** Equals exactly `"Overdue (no promise)"` — pinning the legacy label text
  literally, even though `LatestPromisedDate.HasValue` is true. This is the scenario a rebuild
  developer would be most tempted to "fix" on sight; the fixture exists specifically to catch that.
- **Kind:** edge case
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-011 — Verdict precedence: Promise pending

- **Seam:** Pure function — `AccountabilityRow.Verdict`
- **Business rules pinned:** DR-007
- **Arrange:** `PromiseKept = false`, `PromiseBroken = false`, `IsOverdue = false`,
  `LatestPromisedDate = <a real future date>`.
- **Act:** Read `.Verdict`.
- **Assert (shape):** Equals exactly `"Promise pending"`.
- **Kind:** edge case
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-012 — Verdict precedence: On track (no promise, nothing overdue)

- **Seam:** Pure function — `AccountabilityRow.Verdict`
- **Business rules pinned:** DR-007
- **Arrange:** All four flags/`LatestPromisedDate` at their falsy/null defaults.
- **Act:** Read `.Verdict`.
- **Assert (shape):** Equals exactly `"On track"`.
- **Kind:** edge case
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-013 — GetAccountabilityReportAsync: PromiseKept exact-equality boundary

- **Seam:** Stateful service boundary — `PlanningService.GetAccountabilityReportAsync` (`:269-330`)
- **Business rules pinned:** DR-007 (the `<=` in `t.CompletedUtc.Value.Date <= promised.Date`)
- **Arrange:** In a fresh `TestDb`, create a manager, a team member, a project, and a task with a
  promise note (`IsPromise = true`, `PromisedDate` = an anchor date, e.g. `today + 3 days`), then
  `ChangeStatusAsync(task.Id, Done, managerId)` with `CompletedUtc` landing on **exactly** the same
  calendar date as `PromisedDate` (see seams.md CB-1: this requires pinning "now" — either via an
  injectable clock the rebuild's equivalent must accept, or by choosing the promise/deadline dates
  as offsets from whatever "now" the harness observes at run time, per CB-1's Option 1/3 guidance).
- **Act:** Call `GetAccountabilityReportAsync(projectId)`.
- **Assert (shape):** The single row has `PromiseKept = true`, `PromiseBroken = false`,
  `Verdict == "Kept promise"`.
- **Kind:** boundary
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-014 — GetAccountabilityReportAsync: PromiseBroken exact-boundary (Done but one day late)

- **Seam:** Stateful service boundary — `GetAccountabilityReportAsync`
- **Business rules pinned:** DR-007
- **Arrange:** Same shape as GM-013, but `CompletedUtc`'s date is exactly one day **after**
  `PromisedDate`'s date.
- **Act:** Call `GetAccountabilityReportAsync(projectId)`.
- **Assert (shape):** `PromiseKept = false`, `PromiseBroken = true`, `Verdict == "BROKE promise"`.
- **Kind:** boundary
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-015 — GetAccountabilityReportAsync: not-Done, promise due exactly today is NOT yet broken

- **Seam:** Stateful service boundary — `GetAccountabilityReportAsync`
- **Business rules pinned:** DR-007 (the strict `<` in `promised.Date < now.Date`)
- **Arrange:** A task not in `Done` status, with a promise note whose `PromisedDate`'s date equals
  "today" (the harness's own run-time `now`, not a hardcoded literal — see CB-2).
- **Act:** Call `GetAccountabilityReportAsync(projectId)`.
- **Assert (shape):** `PromiseBroken = false` (same-day promise is not yet broken because the
  comparison is strict `<`, not `<=`) — this pins a genuine, easy-to-get-wrong boundary a rebuild
  might naturally implement as `<=` instead.
- **Kind:** boundary
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-016 — GetAccountabilityReportAsync: not-Done, promise one day overdue IS broken

- **Seam:** Stateful service boundary — `GetAccountabilityReportAsync`
- **Business rules pinned:** DR-007
- **Arrange:** Same as GM-015 but `PromisedDate`'s date is exactly one day before "today."
- **Act:** Call `GetAccountabilityReportAsync(projectId)`.
- **Assert (shape):** `PromiseBroken = true`, `Verdict == "BROKE promise"`.
- **Kind:** boundary
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-017 — GetAccountabilityReportAsync: latest promise supersedes an older one

- **Seam:** Stateful service boundary — `GetAccountabilityReportAsync`
- **Business rules pinned:** DR-007 ("only the most recently created promise note... is used —
  an earlier promise is entirely superseded")
- **Arrange:** A task with two promise notes inserted directly against `PlanningDbContext` (not
  via `AddNoteAsync`, to control `CreatedUtc` explicitly, mirroring the existing
  `Accountability_uses_latest_promise` test): an older note (`CreatedUtc` earlier, `PromisedDate`
  in the past — the shape that *would* compute as broken if it were used) and a newer note
  (`CreatedUtc` later, `PromisedDate` in the future — not yet due).
- **Act:** Call `GetAccountabilityReportAsync(projectId)`.
- **Assert (shape):** `LatestPromisedDate` equals the **newer** note's `PromisedDate`;
  `PromiseBroken = false` (the older, would-be-broken promise is fully superseded, not merged or
  averaged).
- **Kind:** edge case
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-018 — GetAccountabilityReportAsync: sort order with a genuine tie (CQ-024's undocumented tie-break)

- **Seam:** Stateful service boundary — `GetAccountabilityReportAsync`
- **Business rules pinned:** DR-007 (the `OrderByDescending(PromiseBroken).ThenByDescending(IsOverdue).ThenBy(Deadline ?? MaxValue)` sort, `PlanningService.cs:326-328`)
- **Arrange:** Two tasks in the same project, both broken-promise-and-overdue, sharing the
  **identical** `Deadline` value (so all three documented sort keys tie) — differing only in,
  e.g., which was created/added first.
- **Act:** Call `GetAccountabilityReportAsync(projectId)`.
- **Assert (shape):** Record the two rows' resulting order exactly as observed — this **is** the
  golden master CQ-024 asks for regarding undocumented tie-break behavior; do not assume or
  "correct" an expected order — capture whatever the legacy system's real SQLite/EF Core execution
  actually produces for this specific tie shape.
- **Kind:** edge case
- **Verifies backlog item:** BL-008 (Accountability reporting)

### GM-019 — ChangeStatusAsync: same-status transition is a no-op

- **Seam:** Stateful service boundary — `PlanningService.ChangeStatusAsync` (`:184-205`)
- **Business rules pinned:** DR-008
- **Arrange:** A freshly created task (default status `NotStarted`).
- **Act:** Call `ChangeStatusAsync(task.Id, WorkItemStatus.NotStarted, managerId)`.
- **Assert (shape):** `task.StatusHistory` is empty afterward (no `StatusChange` row written) —
  mirrors the existing `ChangeStatus_to_same_status_is_noop` test exactly.
- **Kind:** edge case
- **Verifies backlog item:** BL-004 (Task status transitions and the audit trail)

### GM-020 — ChangeStatusAsync: transitioning into Done sets CompletedUtc

- **Seam:** Stateful service boundary — `ChangeStatusAsync`
- **Business rules pinned:** DR-009
- **Arrange:** A task in `InProgress`.
- **Act:** `ChangeStatusAsync(task.Id, WorkItemStatus.Done, managerId)`.
- **Assert (shape):** Reloaded task has `Status == Done` and `CompletedUtc` is non-null (per
  seams.md CB-1, the literal timestamp value is recorded but not asserted byte-exact — only
  non-null-ness, since no documented rule reads the exact value back).
- **Kind:** boundary
- **Verifies backlog item:** BL-004 (Task status transitions and the audit trail)

### GM-021 — ChangeStatusAsync: moving OUT of Done clears CompletedUtc back to null

- **Seam:** Stateful service boundary — `ChangeStatusAsync`
- **Business rules pinned:** DR-009 — the CQ-018-flagged possibly-surprising half of this rule.
- **Arrange:** A task moved to `Done` first (so `CompletedUtc` is set), then moved again to
  `InProgress`.
- **Act:** `ChangeStatusAsync(task.Id, WorkItemStatus.InProgress, managerId)` after the first
  `Done` transition.
- **Assert (shape):** Reloaded task has `Status == InProgress` and `CompletedUtc == null` — the
  fact that a task was once completed (and when) is unconditionally lost the moment it's reopened,
  pinned exactly as legacy behavior regardless of whether CQ-018 is later decided to be a defect.
- **Kind:** edge case
- **Verifies backlog item:** BL-004 (Task status transitions and the audit trail)

### GM-022 — SetOwnersAsync: replaces the full set, does not append

- **Seam:** Stateful service boundary — `PlanningService.SetOwnersAsync` (`:172-179`)
- **Business rules pinned:** DR-010
- **Arrange:** A task; call `SetOwnersAsync(task.Id, [memberA, managerId])` first.
- **Act:** Call `SetOwnersAsync(task.Id, [memberA])` again (a strict subset, dropping `managerId`).
- **Assert (shape):** After the second call, `TaskOwners` for this task contains exactly
  `[memberA]` — `managerId`'s ownership row is gone, not left alongside the new set — mirrors the
  existing `Task_can_have_multiple_owners` test's second assertion.
- **Kind:** boundary
- **Verifies backlog item:** BL-002 (Objective grouping and the planner grid — owner column)

### GM-023 — ToggleChecklistItemAsync: stamps and clears CompletedUtc both ways

- **Seam:** Stateful service boundary — `PlanningService.ToggleChecklistItemAsync` (`:162-169`)
- **Business rules pinned:** DR-011
- **Arrange:** A checklist item, initially `IsDone = false`.
- **Act:** `ToggleChecklistItemAsync(item.Id, true)`, then `ToggleChecklistItemAsync(item.Id, false)`.
- **Assert (shape):** After the first call, `IsDone == true` and `CompletedUtc` is non-null; after
  the second, `IsDone == false` and `CompletedUtc == null` (non-null-ness only asserted per CB-1's
  Option 2 — the literal timestamp is not business-rule-relevant here).
- **Kind:** edge case
- **Verifies backlog item:** BL-005 (Nested checklist items and grid status badges)

### GM-024 — DeleteProjectAsync cascades to everything under the project

- **Seam:** Stateful service boundary / Data-persistence boundary —
  `PlanningService.DeleteProjectAsync` (`:64-70`), backed by `Project`→`Objective`/`WorkItem`/`Meeting` Cascade
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A project with an objective, a task (under that objective) carrying a checklist
  tree, a progress note, a status-change history row, task owners, and a meeting.
- **Act:** `DeleteProjectAsync(projectId)`.
- **Assert (shape):** Zero rows remain in `Projects`, `Objectives`, `WorkItems`, `ChecklistItems`,
  `ProgressNotes`, `TaskOwners`, and `Meetings` for that project — mirrors
  `DeleteProject_removes_everything_under_it` exactly.
- **Kind:** boundary
- **Verifies backlog item:** BL-010 (Project deletion, cascade)

### GM-025 — DeleteTaskAsync cascades to its checklist/notes/owners/status history

- **Seam:** Stateful service boundary / Data-persistence boundary —
  `PlanningService.DeleteTaskAsync` (`:73-79`), backed by `WorkItem`→`ProgressNote`/`StatusChange`/`ChecklistItem` Cascade and `TaskOwner` Cascade
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A task with a nested checklist (parent + child item), a progress note, a status
  change, and an owner.
- **Act:** `DeleteTaskAsync(task.Id)`.
- **Assert (shape):** Zero rows remain in `WorkItems`, `ChecklistItems`, `ProgressNotes`,
  `TaskOwners` for that task — mirrors `Deleting_task_cascades_to_checklist_and_owners` /
  `DeleteTask_removes_nested_checklist`.
- **Kind:** boundary
- **Verifies backlog item:** BL-009 (Task deletion, cascade)

### GM-026 — Deleting an Objective directly (no PlanningService method exists) sets WorkItem.ObjectiveId to null

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation
  (`_db.Objectives.Remove(...)`), backed by `Objective`→`WorkItem` SetNull (`PlanningDbContext.cs:77-80`)
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A task with `ObjectiveId` set to a real objective.
- **Act:** `t.Db.Objectives.Remove(objective); await t.Db.SaveChangesAsync();` (no
  `PlanningService` method exists for this — confirmed by reading the whole class; this is the
  only way to exercise the rule at all).
- **Assert (shape):** The task row survives; its `ObjectiveId` is now `null`.
- **Kind:** edge case
- **Verifies backlog item:** BL-002 (Objective grouping and the planner grid)

### GM-027 — Deleting an unassigned-elsewhere User (task assignee) directly sets WorkItem.AssigneeId to null

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation, backed by
  `Assignee`(`User`)→`WorkItem` SetNull (`PlanningDbContext.cs:65-68`)
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A team-member `User` who owns no `Project`, has authored no `ProgressNote`/
  `StatusChange` (so no `Restrict` rule blocks the delete), and is set as `AssigneeId` on a task.
- **Act:** `t.Db.Users.Remove(member); await t.Db.SaveChangesAsync();`
- **Assert (shape):** The task survives; `AssigneeId` is now `null`.
- **Kind:** edge case
- **Verifies backlog item:** BL-003 (Task/WorkItem creation and viewing)

### GM-028 — Deleting a Meeting directly sets both WorkItem.DiscoveredInMeetingId and ProgressNote.MeetingId to null

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation, backed by
  `Meeting`→`WorkItem` (discovered-in) SetNull (`:71-74`) and `Meeting`→`ProgressNote` SetNull
  (`:168-171`)
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A meeting linked as both a task's `DiscoveredInMeetingId` and a note's `MeetingId`
  (no `PlanningService.DeleteMeetingAsync` exists — confirmed by reading the whole class).
- **Act:** `t.Db.Meetings.Remove(meeting); await t.Db.SaveChangesAsync();`
- **Assert (shape):** Both the task and the note survive; `DiscoveredInMeetingId` and `MeetingId`
  are both now `null`.
- **Kind:** edge case
- **Verifies backlog item:** BL-006 (Meeting recording and history)

### GM-029 — Deleting a ChecklistItem's Assignee sets ChecklistItem.AssigneeId to null

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation, backed by
  `ChecklistItem`→`Assignee`(`User`) SetNull (`:114-117`)
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A checklist item with `AssigneeId` set to a team member who owns no project and has
  authored no note/status-change.
- **Act:** Remove that `User` directly and save.
- **Assert (shape):** The checklist item survives; `AssigneeId` is now `null`.
- **Kind:** edge case
- **Verifies backlog item:** BL-005 (Nested checklist items and grid status badges)

### GM-030 — Attempting to delete a User who still owns a Project throws (Restrict)

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation, backed by
  `User`→`Project`(Owner) **Restrict** (`:44-47`)
- **Business rules pinned:** no numbered rule — Relationships section; domain-model.md notes this
  is "currently untestable through either UI" since no `DeleteUserAsync` exists — true for the UI,
  but directly testable against `DbContext`.
- **Arrange:** A manager `User` who owns a project.
- **Act:** `t.Db.Users.Remove(manager); await t.Db.SaveChangesAsync();`
- **Assert (shape):** Throws (an EF Core `DbUpdateException` wrapping the SQLite FK-constraint
  violation); the `User` and `Project` rows both survive afterward.
- **Kind:** edge case
- **Verifies backlog item:** BL-001 (Project management)

### GM-031 — Attempting to delete a User who authored a StatusChange throws (Restrict)

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation, backed by
  `User`(ChangedBy)→`StatusChange` **Restrict** (`:190-193`); the equivalent `ProgressNote.Author`
  Restrict rule (`:173-176`) is the same shape and is not separately scenario'd here.
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A manager who performed a status change on some task (owns no project, so only the
  `StatusChange` Restrict rule is in play).
- **Act:** Remove that `User` directly and save.
- **Assert (shape):** Throws; the `User` and `StatusChange` row both survive.
- **Kind:** edge case
- **Verifies backlog item:** BL-004 (Task status transitions and the audit trail)

### GM-032 — Attempting to delete a ChecklistItem that has children throws (self-referencing Restrict)

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation, backed by
  `ChecklistItem.Parent` self-reference **Restrict** (`:109-112`)
- **Business rules pinned:** no numbered rule — Relationships section. **Caveat, read directly
  from clarifications.md's CQ-014**: no application code path (neither desktop UI nor any
  `PlanningService` method) ever performs a single-`ChecklistItem` delete — the only real removal
  path is the whole-`WorkItem` cascade, which removes every checklist row together, sidestepping
  this rule entirely. This scenario pins only the narrow, real, schema-level fact ("this specific
  raw operation throws"); it does **not** establish or reproduce any user-facing "delete one
  sub-tree" feature — CQ-014's broader design question (whether such a feature should exist in the
  rebuild, and what it should do) remains separately open and unresolved by this fixture.
- **Arrange:** A checklist item with at least one child (`ParentId` pointing to it).
- **Act:** `t.Db.ChecklistItems.Remove(parentItem); await t.Db.SaveChangesAsync();`
- **Assert (shape):** Throws; both the parent and child rows survive afterward.
- **Kind:** edge case
- **Verifies backlog item:** BL-005 (Nested checklist items and grid status badges)

### GM-033 — Deleting a User who only owns tasks via TaskOwner cascades their ownership rows away

- **Seam:** Data/persistence boundary — direct `PlanningDbContext` manipulation, backed by
  `TaskOwner`→`User` **Cascade** (`:130-133`) — the many-to-many's *other* FK direction, distinct
  from GM-025's `TaskOwner`→`WorkItem` cascade.
- **Business rules pinned:** no numbered rule — Relationships section
- **Arrange:** A team-member `User` who owns no project, has authored no note/status-change, and
  is listed as a `TaskOwner` on a task (but is not that task's `AssigneeId`).
- **Act:** Remove that `User` directly and save.
- **Assert (shape):** The `WorkItem` itself survives; the `TaskOwner` row linking this user to it
  is gone (cascaded), confirming the Cascade-on-both-FKs behavior from the `User` side
  specifically, distinct from deleting the task itself.
- **Kind:** edge case
- **Verifies backlog item:** BL-009 (Task deletion, cascade)

### GM-034 — PercentComplete: the TotalTasks == 0 special case

- **Seam:** Pure function — `ProjectSummary.PercentComplete` (`Services/Reports.cs:63`)
- **Business rules pinned:** no numbered rule — derived read model
- **Arrange:** A project with zero tasks.
- **Act:** `GetProjectSummaryAsync(projectId)`.
- **Assert (shape):** `PercentComplete == 0` via the explicit `TotalTasks == 0 ? 0 : ...` branch,
  not a `0/0` division.
- **Kind:** boundary
- **Verifies backlog item:** BL-001 (Project management)

### GM-035 — PercentComplete: genuine 0% via division (tasks exist, none Done)

- **Seam:** Pure function — `ProjectSummary.PercentComplete`
- **Business rules pinned:** no numbered rule
- **Arrange:** A project with 3 tasks, none `Done`.
- **Act:** `GetProjectSummaryAsync(projectId)`.
- **Assert (shape):** `PercentComplete == 0.0`, this time via `Math.Round(100.0 * 0 / 3, 1)`, not
  the special-cased branch — confirms the two code paths agree at their boundary.
- **Kind:** edge case
- **Verifies backlog item:** BL-001 (Project management)

### GM-036 — PercentComplete: 100% when every task is Done

- **Seam:** Pure function — `ProjectSummary.PercentComplete`
- **Business rules pinned:** no numbered rule
- **Arrange:** A project with 2 tasks, both `Done`.
- **Act:** `GetProjectSummaryAsync(projectId)`.
- **Assert (shape):** `PercentComplete == 100.0`.
- **Kind:** boundary
- **Verifies backlog item:** BL-001 (Project management)

### GM-037 — PercentComplete: repeating-decimal rounding (1 of 3 done)

- **Seam:** Pure function — `ProjectSummary.PercentComplete`
- **Business rules pinned:** no numbered rule — **directly resolves CQ-020**'s previously-unquoted
  rounding rule.
- **Arrange:** A project with 3 tasks, 1 `Done`.
- **Act:** `GetProjectSummaryAsync(projectId)`.
- **Assert (shape):** `PercentComplete == 33.3` (i.e. `Math.Round(100.0/3, 1)`, truncated/rounded
  to exactly one decimal place, not `33`, not `33.33`, not `34`).
- **Kind:** edge case
- **Verifies backlog item:** BL-001 (Project management)

### GM-038 — PercentComplete: exact-midpoint rounding (1 of 80 done)

- **Seam:** Pure function — `ProjectSummary.PercentComplete`
- **Business rules pinned:** no numbered rule — **resolves CQ-020's midpoint tie-break
  precisely**, pinning `Math.Round`'s default `MidpointRounding.ToEven` (banker's rounding).
- **Arrange:** A project with 80 tasks, 1 `Done` — `100.0 * 1 / 80 = 1.25` exactly, a genuine tie
  at the second decimal place.
- **Act:** `GetProjectSummaryAsync(projectId)`.
- **Assert (shape):** `PercentComplete == 1.2` (rounds to the nearest *even* first-decimal digit,
  not `1.3`, which an `AwayFromZero`/naive-rounding rebuild implementation might produce instead).
- **Kind:** boundary
- **Verifies backlog item:** BL-001 (Project management)

### GM-039 — Dual ownership mechanisms coexist without reconciliation

- **Seam:** Stateful service boundary — `PlanningService.AddTaskAsync` + `SetOwnersAsync`
- **Business rules pinned:** no numbered rule — cites the `TaskOwner` entity finding in
  domain-model.md ("this coexists with (and duplicates) `WorkItem.AssigneeId`... neither
  `PlanningService` nor either desktop app retires the older field") and CQ-005.
- **Arrange:** Create a task with `AssigneeId` set to member A, then call
  `SetOwnersAsync(task.Id, [memberB])` — a completely different person, deliberately excluding A.
- **Act:** Reload the task with both `Assignee` and `Owners` included.
- **Assert (shape):** `AssigneeId == memberA.Id` and `Owners` contains exactly `[memberB]` —
  pinning that the legacy app allows this un-reconciled, simultaneously-inconsistent state without
  any validation error or automatic sync. This scenario deliberately does **not** attempt to
  decide which mechanism is "correct" — that is CQ-005's open DECISION question, not this
  fixture's job.
- **Kind:** edge case
- **Verifies backlog item:** BL-003 (Task/WorkItem creation and viewing)

### GM-040 — AddTaskAsync sets DiscoveredInMeetingId when explicitly passed (service-reachable, UI-unreachable)

- **Seam:** Stateful service boundary — `PlanningService.AddTaskAsync` (`:95-114`)
- **Business rules pinned:** no numbered rule — cites functional-spec.md Named Gap #2 ("neither
  app's UI ever passes `discoveredInMeetingId`") and CQ-012. Unlike the `ProjectStatus` case (see
  "No Legacy Behaviour Exists" below), this **is** reachable — `AddTaskAsync` itself accepts the
  parameter and the existing `Discovered_task_links_to_meeting` test already proves it works
  end-to-end at the service layer; it is only the two desktop UIs that never exercise this path.
- **Arrange:** A meeting.
- **Act:** `AddTaskAsync(projectId, "title", null, null, deadline, isDiscovered: true,
  discoveredInMeetingId: meeting.Id)`.
- **Assert (shape):** The created task has `IsDiscovered == true` and
  `DiscoveredInMeetingId == meeting.Id`.
- **Kind:** edge case
- **Verifies backlog item:** BL-003 (Task/WorkItem creation and viewing)

### GM-041 — LoadSampleDataIfEmpty populates the documented structural shape

- **Seam:** Stateful service boundary — `PlanningService.LoadSampleDataIfEmpty` /
  `DbSeeder.SeedIfEmpty` (`Data/DbSeeder.cs`)
- **Business rules pinned:** no numbered rule — partially answers CQ-023 (structural shape only;
  full verbatim content export is a separate, non-scenario data-dump task — see seams.md CB-5).
- **Arrange:** A fresh, empty database.
- **Act:** `LoadSampleDataIfEmpty()`.
- **Assert (shape):** Exactly 6 `Users`, exactly 3 `Projects`; at least one `WorkItem` with
  `IsDiscovered == true`; at least one `ProgressNote` with `IsPromise == true`; at least one
  `Objective` exists; at least one `ChecklistItem` has a non-null `ParentId` (nesting exists); at
  least one `TaskOwner` row exists. Deliberately **excludes** any assertion on Verdict/Overdue
  outcomes, since `DbSeeder`'s dates are unanchored to a fixed clock (seams.md CB-5).
- **Kind:** boundary
- **Verifies backlog item:** BL-011 (Sample-data lifecycle)

### GM-042 — LoadSampleDataIfEmpty is idempotent

- **Seam:** Stateful service boundary — `LoadSampleDataIfEmpty`
- **Business rules pinned:** no numbered rule
- **Arrange:** A freshly seeded database (from GM-041's arrange+act).
- **Act:** Call `LoadSampleDataIfEmpty()` a second time.
- **Assert (shape):** `Users.Count` is unchanged (still 6) — no duplicate seeding occurs.
- **Kind:** edge case
- **Verifies backlog item:** BL-011 (Sample-data lifecycle)

### GM-043 — ResetToSampleData wipes edits and restores the fresh sample counts

- **Seam:** Stateful service boundary — `DbSeeder.ResetToSampleData`
- **Business rules pinned:** no numbered rule
- **Arrange:** A seeded database with one extra, user-added task (`AddTaskAsync(..., "my extra
  task", ...)`).
- **Act:** `DbSeeder.ResetToSampleData(db)`.
- **Assert (shape):** `Users.Count == 6`, `Projects.Count == 3` (fresh sample restored); no
  `WorkItem` with title `"my extra task"` remains (the edit is gone) — mirrors
  `ResetToSampleData_wipes_edits_and_restores_sample` exactly.
- **Kind:** edge case
- **Verifies backlog item:** BL-011 (Sample-data lifecycle)

## No Legacy Behaviour Exists

- **`Project.Status` ever holding `OnHold`, `Completed`, or `Cancelled`.** `AddProjectAsync`
  (`PlanningService.cs:54-61`) always constructs a `Project` at the struct default (`Active`) and
  accepts no `status` parameter at all — confirmed by reading the full method signature, unlike
  the cascade-delete cases above where a `DbContext`-level path exists even without a
  `PlanningService` method. Critically, **no code anywhere in `PlanningService` branches on
  `ProjectStatus`'s value** — there is no observable behavior difference between `Active` and any
  other status to golden-master; a direct database write of `Status = OnHold` would persist
  trivially but would not exercise any distinguishable *behavior*. This should become a `SCOPE`
  question for `/specclaw:clarify` about whether the rebuild adds a status-changing feature at
  all — it already is one: CQ-011 covers exactly this gap.
- **Successfully deleting a `ChecklistItem` that has children by automatically reparenting or
  otherwise preserving its children/siblings.** Distinct from GM-032 (which pins that the raw
  attempt *throws*): no code path anywhere — not either desktop UI, not any `PlanningService`
  method, not the `Restrict`-configured schema itself — implements an affordance that completes
  such a deletion while keeping the subtree's children alive under a different parent (or as new
  top-level items). The `Restrict` rule only blocks the raw delete outright; it does not provide
  the reparenting behavior CQ-014 asks the rebuild to design fresh. This is exactly the
  already-flagged CQ-014 `DECISION` question, not a gap this harness can fill by observation.

## Rule Coverage Check

1. **DR-001 — Project name required, ≤120 chars** — covered by GM-001.
2. **DR-002 — Task title required, ≤120 chars** — covered by GM-002.
3. **DR-003 — Objective title required, ≤150 chars** — covered by GM-003.
4. **DR-004 — Checklist label required, ≤300 chars** — covered by GM-004.
5. **DR-005 — Note text required, ≤2000 chars** — covered by GM-005.
6. **DR-006 — Note date backward/forward window** — covered by GM-006, GM-007.
7. **DR-007 — Promised-vs-delivered verdict computation (precedence order + sort)** — covered by GM-008
   through GM-018 (11 scenarios: 5 pure-function precedence-branch scenarios, 5 stateful
   boundary/superseding-promise scenarios, 1 sort-tie-break scenario).
8. **DR-008 — Changing a task to its current status is a no-op** — covered by GM-019.
9. **DR-009 — Completion timestamp tracks the Done transition, both ways** — covered by GM-020 (into Done)
   and GM-021 (out of Done).
10. **DR-010 — Setting task owners replaces the full set, does not append** — covered by GM-022.
11. **DR-011 — Toggling a checklist item stamps/clears its completion time** — covered by GM-023.

Every one of domain-model.md's 11 numbered Business Rules has at least one covering scenario; none
were silently dropped. The 20 additional scenarios (GM-024 through GM-043) cover cascade/SetNull/
Restrict delete behavior (10), computed-property boundaries (5), the dual-ownership-mechanism
coexistence (1), the service-reachable-but-UI-unreachable `DiscoveredInMeetingId` path (1), and the
sample-data lifecycle (3) — none of these map to a single numbered rule, so each cites "no numbered
rule" against the specific Entities/Relationships/derived-read-model finding in domain-model.md
that grounds it instead.
