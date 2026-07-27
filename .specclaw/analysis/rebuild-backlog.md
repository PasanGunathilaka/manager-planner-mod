# Rebuild Backlog: Manager Planner / Executive Planning

**Path analyzed:** .
**Date generated:** 2026-07-27
**Source documents:** codebase-report.md, architecture.md, domain-model.md, functional-spec.md

## Backlog

### 1. Project management: create, browse, switch, and summarize

**Maps to capability:** functional-spec.md — "Switch the active project" (header `ComboBox`); "Refresh the selected project's data"; "View project summary counts (Total/Done/In progress/Blocked/Not started/Overdue/% complete)"; "Create a new project" (all Executive Planning Desktop); "Browse all projects (`ListBox`, Name + Description), select one — drives the Planner Grid, Task+Notes and Accountability windows"; "Create a project — Name/Description `TextBox`es + '➕ Add project' `Button`" (Projects window); "View ▸ *Refresh* — `RefreshCommand` → `Refresh`" (menu bar) — all Manager Planner Desktop.

**Merge rationale:** These six bullets are small, tightly-coupled reads/writes on one root `Project` entity, duplicated across both front ends but funneling into the same `PlanningService` methods. architecture.md's L3 finding states "every command... calls `_service.*` directly" for both ViewModels with "no repository/abstraction layer... between the VM and the Core service" — treating select/refresh/summarize/create as one root-entity feature avoids six near-identical entries for what is functionally one CRUD-and-selection surface.

**Depends on:** None — `Project` is the hierarchy root (domain-model.md ERD: `PROJECT ||--o{ OBJECTIVE`, `PROJECT ||--o{ WORKITEM`, `PROJECT ||--o{ MEETING`).

**Acceptance basis (domain-model.md):**
- Entity: "Project — ... 'A body of work the Manager plans and tracks. Owns many tasks and meetings.' Fields: `Id`, `Name`, `Description?`, `Status` (`ProjectStatus`), `CreatedUtc`, `OwnerId`/`Owner` (FK to `User`); navigation `Objectives`, `Tasks`, `Meetings`."
- Business Rule 1: "Project name required, ≤120 chars — `PlanningRules.ValidateProjectName`... Rejects an empty/whitespace-only name and any name over `MaxProjectName` (120) characters."
- Relationship: "`User` → `Project` (owns), `Restrict`. `Project.OwnerId` is non-nullable — a Manager cannot be deleted while still owning projects (no `DeleteUserAsync` exists in `PlanningService` at all...)."
- Enumeration: "`ProjectStatus`... `Active = 0`, `OnHold = 1`, `Completed = 2`, `Cancelled = 3`... no code path in `PlanningService` or either desktop app currently lets the user change a project's `Status` after creation (`AddProjectAsync` always creates it as the struct default, `Active`)."
- Derived read model: "`ProjectSummary` carries per-project task counts (`TotalTasks`, `Done`, `InProgress`, `Blocked`, `NotStarted`, `Overdue`, `Discovered`, and a computed `PercentComplete`)."

**Verification inputs needed:**
- A golden-master capture of `GetProjectSummaryAsync`'s exact `PercentComplete` computation for known task-count combinations — domain-model.md states only that it is "a computed `PercentComplete`," without the rounding/truncation rule; the rebuild needs the legacy app's actual displayed output for representative inputs, not just equivalent logic.
- codebase-report.md's Risks/Tech-Debt: "No EF Core migrations exist anywhere in the repo... `PlanningDbContextFactory.Create` only calls `ctx.Database.EnsureCreated()`." Since `Project` is the first persisted entity, a human must decide the rebuild's schema-evolution strategy — the source has no migration path to observe as a reference.
- A human product decision on whether `ProjectStatus`'s three unused values (`OnHold`/`Completed`/`Cancelled`) should gain a UI in the rebuild — domain-model.md confirms they are "read by nothing in the running apps beyond their default value" today; this is a deliberate scope call, not a defect to silently fix.

---

### 2. Objective grouping and the planner grid

**Maps to capability:** functional-spec.md (Manager Planner Desktop, Planner Grid window) — "Add an objective to the selected project — `TextBox` + 'Add' `Button` → `AddObjectiveCommand`"; "View tasks grouped by objective; each row shows owners/status and a nested, expandable checklist (`TreeView`...)."

**Merge rationale:** Both bullets describe the same Planner Grid window/feature — adding an objective and viewing the grid it immediately populates are inseparable in the source (`PlannerGridView.axaml`); splitting them would create an "add" item with no corresponding "view" counterpart.

**Depends on:** 1 (`Objective.ProjectId` is a required FK to `Project` — a Project must exist before an Objective can be created).

**Acceptance basis (domain-model.md):**
- Entity: "Objective — ... 'A goal within a project ... Sits between Project and WorkItem so work is grouped the way a manager plans it: Project → Objective → Task.' Fields: `Id`, `Title`, `KeyResult?` (...), `SortOrder`, `ProjectId`/`Project`; navigation `Tasks`."
- Business Rule 3: "Objective title required, ≤150 chars — `ValidateObjectiveTitle` (:37-43). Mechanical: no stated reason for 150 vs. the task/project limit of 120."
- Relationships: "`Project` → `Objective`/`WorkItem`/`Meeting`, `Cascade`." and "`Objective` → `WorkItem` (optional), `SetNull`. A task's objective grouping is optional; a task survives its objective being removed (though no UI path currently removes a single objective — see Named Gaps)."
- Owner-column context: "TaskOwner — ... 'Join entity for the many-to-many between tasks and their owners, so a task can be owned by several people.'" and Business Rule 10: "Setting task owners replaces the full set, it does not append — `SetOwnersAsync`... removes every existing `TaskOwner` row for the task before adding the new set."

**Verification inputs needed:**
- functional-spec.md Named Gap #4: "No UI sets/changes task owners... The Planner Grid's 'Owner' column (`TaskRowVm.OwnersText`) is read-only and reflects only seeded data — there is no control in either app to add, remove, or change a task's owners." A human must decide whether the rebuild preserves this read-only display or adds owner-editing — no legacy UI exists to observe for the latter.
- codebase-report.md's Risks/Tech-Debt: "v2 has extra tables (Objectives, ChecklistItems, TaskOwners), so it uses its own database file" (quoted from `ManagerPlanner.Desktop/App.axaml.cs`) — relevant because `Objective` is a v2-only entity; the schema-versioning decision flagged in item 1 applies here too.

---

### 3. Task (WorkItem) creation and viewing

**Maps to capability:** functional-spec.md — "View all tasks for the project in a read-only `DataGrid`... and select one (drives the Meetings & Notes tab's note list)"; "Add a task — title, assignee dropdown ..., deadline `DatePicker`, optional description, 'Discovered in a meeting' `CheckBox`" (Executive Planning Desktop); "Add a task inline under a specific objective — per-group ... `TextBox` + 'Add task' `Button`"; "Select a task — clicking the task cell ... surfaces/focuses the Task+Notes window" (Manager Planner Desktop, Planner Grid).

**Merge rationale:** Both apps expose "create a `WorkItem`" and "view/select a `WorkItem`" against the same entity and the same `PlanningService.AddTaskAsync`, differing only in which fields are exposed (full form vs. a coarse inline add) — architecture.md confirms both ViewModels call `_service.*` directly for this. Treated as one entity-level feature; the field-exposure asymmetry is carried as an explicit acceptance nuance below rather than silently merged away.

**Depends on:** 1, 2 (`WorkItem.ProjectId` is a required FK; `WorkItem.ObjectiveId` is optional but Manager Planner Desktop's *only* add-task affordance is inline under an already-existing Objective group).

**Acceptance basis (domain-model.md):**
- Entity: "WorkItem (the 'task') — ... 'A unit of work under a project, assigned to a team member with a deadline. ... A task can be "discovered" during a meeting — in which case `DiscoveredInMeetingId` is set.' Fields: `Id`, `Title`, `Description?`, `Status` (`WorkItemStatus`), `Deadline?`, `CreatedUtc`, `CompletedUtc?`, `IsDiscovered`; FKs `ProjectId`/`Project`, `ObjectiveId?`/`Objective`, `AssigneeId?`/`Assignee`, `DiscoveredInMeetingId?`/`DiscoveredInMeeting`."
- Business Rule 2: "Task title required, ≤120 chars — `ValidateTaskTitle`... test `AddTask_rejects_empty_and_overlong_titles` exercises both branches."
- Relationships: "`User` (assignee) → `WorkItem`, `SetNull`. Removing a user un-assigns their tasks rather than deleting them — the v1 single-assignee link." and "`Meeting` → `WorkItem` (discovered-in), `SetNull`. A discovered task keeps existing if its originating meeting is deleted."
- Enumeration: "`WorkItemStatus`... `NotStarted = 0`, `InProgress = 1`, `Blocked = 2`, `Done = 3`."

**Verification inputs needed:**
- functional-spec.md Named Gap #6: "`ManagerPlanner.Desktop`'s inline 'add task' never exposes an assignee or a custom deadline. `MainViewModel.OnAddTask`... always calls `AddTaskAsync` with `assigneeId: null` and a hardcoded `DateTime.UtcNow.AddDays(7)` deadline." A human decision is needed on whether the rebuild's single task-creation feature preserves this narrower affordance as a distinct fast-add path or unifies both apps into one full form — the docs describe the asymmetry but not which is canonical.
- functional-spec.md Named Gap #2: "`WorkItem.DiscoveredInMeetingId` is never set through either app's UI" — the checkbox only sets `IsDiscovered = true`. Since the legacy app never exercises this link, there is no golden master to reproduce; a human must decide whether the rebuild wires it up or intentionally leaves it dormant.

---

### 4. Task status transitions and the audit trail

**Maps to capability:** functional-spec.md — "Set a task's status — four buttons ('Not started'/'In progress'/'Blocked'/'Mark done')..." (Executive Planning Desktop); "Task ▸ *Mark selected Done*" and "✔ Mark Done" button in the Task+Notes window (Manager Planner Desktop).

**Merge rationale:** All three surfaces call the same `PlanningService.ChangeStatusAsync` method and produce the same `StatusChange` audit row and `CompletedUtc` side effect; they differ only in how many of the four `WorkItemStatus` values each UI exposes as buttons (a fidelity nuance captured below, not merged away).

**Depends on:** 3 (a `WorkItem` must exist before its status can change).

**Acceptance basis (domain-model.md):**
- Entity: "StatusChange — ... 'Immutable audit record of a task status transition. Gives the Manager a defensible history of when work actually moved forward (or stalled).' Fields: `Id`, `FromStatus`, `ToStatus`, `ChangedUtc`, `Reason?`; FKs `WorkItemId`/`WorkItem`, `ChangedById`/`ChangedBy`."
- Business Rule 8: "Changing a task to its current status is a no-op — `ChangeStatusAsync`: `if (task.Status == newStatus) return;` before any `StatusChange` row is written. Confirmed by test `ChangeStatus_to_same_status_is_noop`."
- Business Rule 9: "Completion timestamp tracks the Done transition, both ways — `ChangeStatusAsync`: `task.CompletedUtc = newStatus == WorkItemStatus.Done ? DateTime.UtcNow : null;` — moving a task *out* of `Done` clears `CompletedUtc` back to `null`."
- Relationship: "`User` (author/changed-by) → `ProgressNote`/`StatusChange`, `Restrict`. The Manager who wrote a note or changed a status cannot be deleted while that record exists."

**Verification inputs needed:**
- A human product decision on the exposed-affordance asymmetry: Executive Planning Desktop exposes all four `WorkItemStatus` values as buttons, while functional-spec.md documents only a single "Mark selected Done" command for Manager Planner Desktop, with no bullet describing a UI path back to `NotStarted`/`InProgress`/`Blocked` in that shell. Neither document states whether this is intentional; the rebuild needs a human call on whether to unify or preserve the asymmetry.
- None beyond the acceptance criteria above for the no-op and completion-timestamp rules themselves — both are fully specified mechanically and covered by named tests quoted in domain-model.md.

---

### 5. Nested checklist items and grid status badges

**Maps to capability:** functional-spec.md — "Tick/untick a nested checklist item — `CheckBox` in the `TreeView` bound to `ChecklistItemVm.IsDone` → `PlanningService.ToggleChecklistItemAsync`"; "Visual-only flags computed client-side: 'OVERDUE' badge (deadline passed, not Done) and '⚑ discovered' badge (`IsDiscovered`)" (Manager Planner Desktop, Planner Grid).

**Merge rationale:** Both are small, read/toggle affordances layered on the same grid row and computed client-side rather than backed by their own window or command surface — the badge logic reads the same `Deadline`/`Status`/`IsDiscovered` fields the checklist toggle sits beside.

**Depends on:** 3, 4 (`ChecklistItem.WorkItemId` requires a `WorkItem`; the OVERDUE badge is computed from that `WorkItem`'s `Deadline` and `Status`, i.e. item 4's status logic).

**Acceptance basis (domain-model.md):**
- Entity: "ChecklistItem — ... 'A nested progress item under a task — the "checklist" column in the planner grid. Items form a tree via `ParentId` ..., each individually tickable and optionally owned by a person.' Fields: `Id`, `Label`, `IsDone`, `SortOrder`, `CompletedUtc?`; FKs `WorkItemId`/`WorkItem`, `ParentId?`/`Parent`/`Children`, `AssigneeId?`/`Assignee`."
- Business Rule 4: "Checklist label required, ≤300 chars — `ValidateChecklistLabel` (:45-51)."
- Business Rule 11: "Toggling a checklist item stamps/clears its completion time — `ToggleChecklistItemAsync`: `item.CompletedUtc = isDone ? DateTime.UtcNow : null;`."
- Relationship: "`ChecklistItem.Parent` self-reference, `Restrict` — explicitly *not* `Cascade`, per the code comment: 'Restrict (children removed in app code) to avoid multiple cascade paths on SQLite.'"

**Verification inputs needed:**
- functional-spec.md Named Gap #9 / domain-model.md's cross-reference: "Whether removing one nested sub-tree while preserving siblings would work is unconfirmed by any code path read this session" — the `Restrict` self-reference rule is never exercised on its own in the legacy app (only whole-`WorkItem` cascade ever removes checklist rows). Because the legacy system never runs this path, **no golden master exists for it**; a human must define the intended single-subtree-delete behavior for the rebuild rather than "faithfully reproduce" something that has never executed.
- functional-spec.md Named Gap #5: "No UI adds a new checklist item... A manager can tick/untick *existing* checklist items... but cannot add a new one from either app's UI." A human decision is needed on whether the rebuild should add checklist-item-creation, since no legacy UI exists to observe as a reference.

---

### 6. Meeting recording and history (Executive Planning Desktop)

**Maps to capability:** functional-spec.md — "Record a meeting — title, type dropdown (`VideoCall`/`PhysicalMeeting`/`PhoneCall`), participant dropdown, date picker → `AddMeetingCommand` → `AddMeetingAsync`"; "Browse the project's meeting history (read-only `ListBox`)."

**Merge rationale:** Recording a meeting and browsing the resulting history are the same small feature area on the same tab, both reading/writing the single `Meeting` entity via adjacent controls in `MainWindow.axaml`'s Meetings & Notes tab.

**Depends on:** 1 (`Meeting.ProjectId` is a required FK to `Project`; `Meeting` does not depend on `Objective` or `WorkItem`).

**Acceptance basis (domain-model.md):**
- Entity: "Meeting — ... 'A recorded conversation (video/physical/phone) between the Manager and a team member. Notes captured during the meeting hang off this record, giving the Manager a per-meeting history to cross-check what was promised versus what was delivered.' Fields: `Id`, `Title`, `Type` (`MeetingType`), `MeetingDate`; FKs `ProjectId`/`Project`, `ParticipantId?`/`Participant`; navigation `Notes`, `DiscoveredTasks`."
- Enumeration: "`MeetingType`... `VideoCall = 0`, `PhysicalMeeting = 1`, `PhoneCall = 2`. Doc comment: 'How the manager met the team member.'"
- Relationship: "`Meeting` → `ProgressNote` (optional), `SetNull`. A note survives its meeting being deleted." and "`Meeting` → `WorkItem` (discovered-in), `SetNull`."

**Verification inputs needed:**
- functional-spec.md Named Gap #1: "`ManagerPlanner.Desktop` has no Meeting-recording capability at all... the newer, actively-published app... offers no UI to record a meeting, browse meeting history, or link a note to one." A human product decision is required: does the rebuild carry Meeting recording into both shells' successors, or preserve this as an intentional feature-tier split between the two apps? The source documents describe the asymmetry but do not state which app's behavior is authoritative for the rebuild.
- A golden-master capture of the exact `MeetingType` display strings shown in the dropdown (e.g. whether "VideoCall" renders as "Video Call" or verbatim) — none of the four documents quote the rendered label text, only the enum member names.

---

### 7. Progress notes and promise tracking

**Maps to capability:** functional-spec.md — "Select a task from the task dropdown"; "Add a progress note — 'What did they say?' text box, 'This is a promise' `CheckBox`, a promised-date `DatePicker`..., an optional 'link to meeting' dropdown → `AddNoteCommand` → `AddNoteAsync`"; "View a task's note history in a read-only `DataGrid`" (Executive Planning Desktop); "View the selected task's title and its full, date-ordered note timeline"; "Add a dated progress note — date `DatePicker`, 'is a promise' `CheckBox`, promised-date `DatePicker`, free-text box → `AddNoteCommand`" (Manager Planner Desktop, Task+Notes window).

**Merge rationale:** Both apps' note-selection, note-adding, and note-history-viewing bullets operate on the single `ProgressNote` entity via the same `AddNoteAsync`/validation pair (`ValidateNoteText`/`ValidateNoteDate`) — treated as one feature since the note-taking and note-viewing are the same read/write surface in both UIs.

**Depends on:** 3, 6 (`ProgressNote.WorkItemId` is a required FK to `WorkItem`; `MeetingId?` is optional but Workflow 1 in functional-spec.md sequences "Records a meeting" before "Adds a progress note" as the canonical path).

**Acceptance basis (domain-model.md):**
- Entity: "ProgressNote — ... 'A note the Manager records against a task — typically during a meeting — capturing what the team member said. This is the heart of the accountability feature: the Manager can flag that the member *promised* something by a certain date, then later cross-check promise vs delivery.' Fields: `Id`, `Text`, `CreatedUtc`, `NoteDate`, `IsPromise`, `PromisedDate?`; FKs `WorkItemId`/`WorkItem`, `MeetingId?`/`Meeting`, `AuthorId`/`Author`."
- Business Rule 5: "Note text required, ≤2000 chars — `ValidateNoteText`... The empty-text message ('The note is empty — type what was said before saving.') states the *intent*... but the 2000-character ceiling itself is mechanical/unexplained."
- Business Rule 6: "A note can only be dated within a fixed backward/forward window — `ValidateNoteDate` (`NoteBackdateMonths = 1`). Rejects a note dated more than one month before today... and rejects any note dated after today."
- Relationships: "`WorkItem` → `ProgressNote`/`StatusChange`/`ChecklistItem`, `Cascade`." and "`User` (author/changed-by) → `ProgressNote`/`StatusChange`, `Restrict`."

**Verification inputs needed:**
- A golden-master capture of the exact validation error message text at the overlong-text and date-window boundaries — domain-model.md quotes only the empty-text message verbatim ("The note is empty — type what was said before saving."); the 2000-character and one-month-window rejection messages are described but not quoted, so exact UI wording needs to be captured from the running legacy app for a faithful rebuild.

---

### 8. Accountability reporting (promised-vs-delivered verdicts)

**Maps to capability:** functional-spec.md — "Accountability tab: view the promised-vs-delivered report for the selected project... most-at-risk sorted first" (Executive Planning Desktop); "Accountability Report window: View the promised-vs-delivered report across **all** projects... This window is read-only; no commands" (Manager Planner Desktop).

**Merge rationale:** Both are the same `Verdict` computation exposed at two scopes (single-project vs. all-projects) via `GetAccountabilityReportAsync`/`GetAccountabilityForAllProjectsAsync` — one business-logic feature, two read-only views of it.

**Depends on:** 3, 4, 7 (the `Verdict` is derived from a `WorkItem`'s `Status`/`Deadline`/`CompletedUtc` — items 3–4 — cross-checked against its latest `ProgressNote` promise — item 7).

**Acceptance basis (domain-model.md):**
- Business Rule 7 (quoted in full, this is the core acceptance criterion): "Promised-vs-delivered verdict computation — ... For each task, only the **most recently created** promise note (`IsPromise && PromisedDate.HasValue`, ordered by `CreatedUtc` descending) is used — an earlier promise is entirely superseded... The verdict is evaluated in this exact precedence order": `PromiseKept` → `PromiseBroken` → `IsOverdue` → "Promise pending" → "On track". "Rows are then sorted broken-first, then overdue-first, then soonest-deadline-first... 'Most at-risk first: broken promises, then overdue, then the rest.'"
- The flagged code-level nuance, quoted directly: "`IsOverdue` is computed independently of whether a promise exists, and is checked *before* `LatestPromisedDate.HasValue` in the `Verdict` getter — so a task whose own deadline has passed but which *does* carry a promise not yet due (not yet 'broken') is labeled 'Overdue (no promise)' even though a promise is in fact on record. This is a direct reading of the code's evaluation order, not a guess."
- Derived read model: "`AccountabilityRow` and `ProjectSummary` are **not** EF-mapped entities... They are computed on-the-fly by `PlanningService.GetAccountabilityReportAsync`/`GetProjectSummaryAsync` from live `WorkItem`/`ProgressNote` rows, on every call, and thrown away after rendering."

**Verification inputs needed:**
- Golden-master input/output pairs (task status + deadline + promise history → exact `Verdict` string and sort position) covering every branch of the precedence order **and specifically the flagged nuance above** — this exact-order quirk is easy for a rebuild developer to "fix" as if it were a bug, silently breaking behavioral equivalence with the legacy system. This is the single highest-priority verification input in this backlog: it cannot be derived from the acceptance-basis text alone, only from observing (or being told to preserve) the legacy system's actual output for edge-case inputs.
- A golden-master sample of the sort order for a project with a mix of broken/overdue/pending/kept/on-track tasks, to confirm the rebuild's tie-breaking matches the legacy's exactly (the doc states the three sort keys but not tie-breaking behavior beyond them).

---

### 9. Task deletion (cascade)

**Maps to capability:** functional-spec.md — "Task ▸ *Delete selected task* — `DeleteTaskCommand` → `DeleteTask`: confirmation dialog, then `PlanningService.DeleteTaskAsync` (cascades to checklist/notes/owners)" (Manager Planner Desktop only — Executive Planning Desktop has no task-delete UI, per Named Gaps).

**Depends on:** 3, 4, 5, 7 (deleting a `WorkItem` cascades to its `StatusChange` history — item 4 — `ChecklistItem` tree — item 5 — and `ProgressNote`s — item 7).

**Acceptance basis (domain-model.md):**
- Relationship: "`WorkItem` → `ProgressNote`/`StatusChange`/`ChecklistItem`, `Cascade`. Deleting a task removes its full note history, status audit trail, and checklist tree in one operation — confirmed by `PlanningService.DeleteTaskAsync` and tests `Deleting_task_cascades_to_checklist_and_owners` / `DeleteTask_removes_nested_checklist`."
- Relationship: "`WorkItem` ↔ `User` via `TaskOwner`, `Cascade` on both FKs... confirmed by `PlanningService.SetOwnersAsync`... and test `Task_can_have_multiple_owners`."

**Verification inputs needed:**
- None beyond the acceptance criteria above — this cascade path is directly exercised by named tests quoted in domain-model.md (`Deleting_task_cascades_to_checklist_and_owners`, `DeleteTask_removes_nested_checklist`), so no external format/DLL/COM dependency or unobserved golden-master behavior applies to this item specifically.

---

### 10. Project deletion (cascade)

**Maps to capability:** functional-spec.md — "**Delete the selected project** — '🗑 Delete selected' `Button` → `DeleteProjectCommand` (confirmation dialog first; cascades to everything under the project)" (Manager Planner Desktop, Projects window only — Executive Planning Desktop has no delete UI at all for either projects or tasks, per Named Gaps).

**Depends on:** 2, 3, 6 (and transitively 4, 5, 7, 9) — deleting a `Project` cascades to every `Objective`, `WorkItem`, and `Meeting` beneath it, and transitively everything those own.

**Acceptance basis (domain-model.md):**
- Relationship: "`Project` → `Objective`/`WorkItem`/`Meeting`, `Cascade`. Deleting a project removes every objective, task and meeting under it in one operation — confirmed by `PlanningService.DeleteProjectAsync` (a single `_db.Projects.Remove(p)` call, no manual cleanup) and by tests `Deleting_project_cascades_to_tasks_and_notes` / `DeleteProject_removes_everything_under_it`."

**Verification inputs needed:**
- None beyond the acceptance criteria above — this cascade path is directly exercised by named tests quoted in domain-model.md, and no external file format, DLL, or COM semantics are flagged anywhere in codebase-report.md's Risks/Tech-Debt for this item.

---

### 11. Sample-data lifecycle (load / reset)

**Maps to capability:** functional-spec.md — "File ▸ *Load sample data* — `LoadSampleDataCommand`... seeds only if the database is currently empty, otherwise shows a message dialog"; "File ▸ *Reset to sample data…* — `ResetSampleDataCommand`... confirmation dialog, then wipes and reseeds via `DbSeeder.ResetToSampleData`" (Manager Planner Desktop only).

**Depends on:** 1–10 (seeding populates every entity type modeled by every prior item — `User`, `Project`, `Objective`, `WorkItem`, `ChecklistItem`, `TaskOwner`, `Meeting`, `ProgressNote`).

**Acceptance basis (domain-model.md / codebase-report.md):**
- codebase-report.md: "The seeded sample data in `DbSeeder.cs`... suggests the tool targets at least two different management contexts... internal engineering-style tracking ('Q3 Platform Migration'...) and external account/relationship management ('Key Account — Tracsis' project with an objective titled 'Build relationships beyond Chris' and a stakeholder-mapping nested checklist naming roles like 'Jenny — Head of QA' and 'Damian — Chris's boss')."
- functional-spec.md workflow "Loading vs. resetting sample data": `HasAnyData()` gates a no-op "load" against existing data with a message dialog; "Reset" always confirms, then wipes and reseeds via `DbSeeder.ResetToSampleData`.

**Verification inputs needed:**
- A full golden-master export of `DbSeeder.cs`'s complete seeded dataset (every `Project`/`Objective`/`WorkItem`/`ChecklistItem`/note/text string) — the four documents quote only representative excerpts (e.g. "Design new database schema," the Tracsis stakeholder names), not the full dataset. The rebuild needs the complete content to reproduce identical "Load"/"Reset" sample data, not just an equivalent-looking one.

---

### 12. Multi-window desktop shell and MDI chrome (Manager Planner Desktop only)

**Maps to capability:** functional-spec.md — "Window ▸ *Cascade* / *Tile*"; "Window ▸ *Show Projects* / *Show Planner Grid* / *Show Notes* / *Show Accountability Report*"; "Drag the title bar... to reposition a window"; "Double-click the title bar, or the `PART_Max` button, to maximize/restore"; "Drag the bottom-right resize grip... to resize"; "Minimize... and close... both simply hide the window (`IsVisible = false`)... not a taskbar."

**Merge rationale:** All of these are the same MDI-shell chrome (`MdiHost`/`MdiWindow`) that hosts the Projects/Planner Grid/Task+Notes/Accountability windows built in items 1, 2, 3, 6, 7, 8 — none carries its own business/domain acceptance rule (domain-model.md's Entities/Business Rules/Enumerations sections say nothing about window chrome), so they are grouped as one shell-infrastructure feature rather than split into five thin UI items.

**Depends on:** 1, 2, 3, 6, 7, 8 (the shell exists to host the Projects, Planner Grid, Task+Notes, and Accountability windows built in those items — there is nothing to cascade/tile/show without them).

**Acceptance basis (domain-model.md):** None — this is UI-shell chrome with no backing entity, business rule, or enumeration in domain-model.md; its acceptance basis instead comes directly from functional-spec.md's "MDI window chrome" bullets quoted above, and from codebase-report.md's description of the implementation below.

**Verification inputs needed:**
- codebase-report.md's Risks/Tech-Debt, quoted in full: "Bespoke UI-framework code with no test safety net. `ManagerPlanner.Desktop/Controls/MessageBox.cs` and `Controls/MdiWindow.cs` hand-roll modal dialogs and MDI window chrome... rather than using Avalonia's built-in `Window`/dialog primitives. This is functional based on the code read, but bespoke chrome logic like this tends to accumulate edge-case bugs (nothing in the read code handles keyboard navigation or persisting z-order across restarts) that are easy to miss without any UI-level test coverage." Because there is **zero automated test coverage** for this component (architecture.md L4: "none of this state is exercised by any test"), a human must supply manual/golden-master captures of exact drag/resize/maximize/restore/z-order behavior — static analysis of the source cannot establish runtime interaction fidelity.
- functional-spec.md Named Gap #7: "Minimized/closed MDI child windows have no taskbar-equivalent... This works as designed, but a first-time user... has no other visible way to bring it back." A human product decision is needed on whether the rebuild preserves this limitation or adds a taskbar-equivalent.

---

### 13. Application chrome: Exit and About

**Maps to capability:** functional-spec.md — "File ▸ *Exit* — `Click="OnExit"` → `MainWindow.axaml.cs:46` → `Close()`"; "Help ▸ *About* — `Click="OnAbout"`... → a static About dialog (name/version blurb, no dynamic data)" (Manager Planner Desktop).

**Merge rationale:** Both are trivial, one-line application-chrome commands with no data dependency of their own — merging avoids two backlog items for a `Close()` call and a static text dialog.

**Depends on:** None substantively — cosmetic application chrome with no entity or business-rule dependency, though it is conventionally part of the same shell built in item 12.

**Acceptance basis (domain-model.md):** None — no entity, business rule, or enumeration in domain-model.md governs Exit or About; the acceptance basis is the functional-spec.md quotes above in full (a plain window close, and "a static About dialog (name/version blurb, no dynamic data)").

**Verification inputs needed:**
- None beyond the acceptance criteria above — no external dependencies or golden-master-only behavior identified for this item; the About dialog is explicitly documented as static content only.

## Sequencing Rationale

Ordering follows the entity dependency chain read directly from domain-model.md's Relationships/ERD section, cross-checked against architecture.md's L3 Components diagram (`coreServices --> coreData --> coreDomain`, i.e. the shared `ExecutivePlanning.Core` domain/data layer underlies both GUI containers) and functional-spec.md's Workflows section.

`Project` is the hierarchy root — domain-model.md's ERD shows `PROJECT ||--o{ OBJECTIVE`, `PROJECT ||--o{ WORKITEM`, and `PROJECT ||--o{ MEETING` all originating from it — so item 1 (Project management) precedes everything else, matching architecture.md's L1 finding that the Manager's whole workflow starts by "plans projects, breaks them into tasks... records what each member says." Item 2 (Objective) comes next because, while `Objective` is optional for a `WorkItem` ("`Objective` → `WorkItem` (optional), `SetNull`"), Manager Planner Desktop's *only* add-task UI is the Planner Grid's per-objective-group inline form (functional-spec.md: "Add a task inline under a specific objective") — so an Objective must already exist for that shell's task-creation flow to be usable, even though the FK itself is nullable. Item 3 (Task/WorkItem) follows, since it is, per domain-model.md, "the hub of the whole accountability feature — every other tracking entity (`ProgressNote`, `StatusChange`, `ChecklistItem`, `TaskOwner`) hangs directly off it." Items 4 (status/audit) and 5 (checklist) both require a `WorkItem` to exist first (`WorkItemId` FKs) and are peers of each other with no cross-dependency. Item 6 (Meeting) requires only `Project` and can in principle sit anywhere after item 1, but is placed after the Task cluster because functional-spec.md's Workflow 1 ("Recording what was said and tracking a promise") sequences "Records a meeting" before "Selects a task... Adds a progress note" as the canonical path, and because `ProgressNote.MeetingId` is optional — Meeting is a prerequisite only for the *documented* workflow, not a hard FK requirement. Item 7 (Progress notes) depends on both 3 and 6 for exactly this reason. Item 8 (Accountability) is sequenced after 3/4/7 because its `Verdict` computation reads `WorkItem.Status`/`Deadline`/`CompletedUtc` (set by item 4) cross-checked against the latest `ProgressNote` promise (item 7) — it is a pure read-model with nothing to compute until those exist. Items 9 and 10 (Task deletion, then Project deletion) are sequenced last among data-model items because their cascade behavior is a superset that touches every entity introduced by items 2–8; testing/rebuilding deletion meaningfully requires those child entities to already be modeled. Item 11 (sample-data lifecycle) is sequenced after all data-model items because `DbSeeder` populates every entity type from `User` through `TaskOwner` — it cannot be built, let alone verified, before the schema it seeds exists. Items 12 (MDI shell chrome) and 13 (app chrome) are placed last because, per domain-model.md, no entity, business rule, or enumeration governs either — they are UI-shell infrastructure that hosts the feature windows built in earlier items (functional-spec.md's Window menu bullets literally "Show Projects"/"Show Planner Grid"/"Show Notes"/"Show Accountability Report," i.e. windows built in items 1, 2, 3/6/7, and 8) rather than domain-model-driven features in their own right.

## Coverage Check

**Executive Planning Desktop:**
- "Switch the active project" — covered by item 1.
- "Refresh the selected project's data" — covered by item 1.
- "View project summary counts" — covered by item 1.
- "Create a new project" — covered by item 1.
- "View all tasks for the project... and select one" — covered by item 3.
- "Set a task's status" (four buttons) — covered by item 4.
- "Add a task" — covered by item 3.
- "Record a meeting" — covered by item 6.
- "Browse the project's meeting history" — covered by item 6.
- "Select a task (dropdown) to view/add progress notes" — covered by item 7.
- "Add a progress note" — covered by item 7.
- "View a task's note history" — covered by item 7.
- "Accountability tab: view the promised-vs-delivered report" — covered by item 8.

**Manager Planner Desktop — menu bar:**
- "File ▸ Load sample data" — covered by item 11.
- "File ▸ Reset to sample data…" — covered by item 11.
- "File ▸ Exit" — covered by item 13.
- "View ▸ Refresh" — covered by item 1.
- "Task ▸ Mark selected Done" — covered by item 4.
- "Task ▸ Delete selected task" — covered by item 9.
- "Window ▸ Cascade / Tile" — covered by item 12.
- "Window ▸ Show Projects / Show Planner Grid / Show Notes / Show Accountability Report" — covered by item 12.
- "Help ▸ About" — covered by item 13.
- Toolbar and Window bar buttons — excluded as separate capabilities: functional-spec.md states directly, "Toolbar and Window bar... duplicate the same commands/handlers as clickable buttons" as the menu-bar items above — these are alternate affordances of already-covered items (1, 4, 9, 12), not distinct capabilities.

**Manager Planner Desktop — Projects window:**
- "Browse all projects... select one" — covered by item 1.
- "Create a project" — covered by item 1.
- "Delete the selected project" — covered by item 10.

**Manager Planner Desktop — Planner Grid window:**
- "Add an objective" — covered by item 2.
- "View tasks grouped by objective; each row shows owners/status and a nested, expandable checklist" — covered by item 2.
- "Add a task inline under a specific objective" — covered by item 3.
- "Select a task" — covered by item 3.
- "Tick/untick a nested checklist item" — covered by item 5.
- "Visual-only flags... 'OVERDUE' badge... and '⚑ discovered' badge" — covered by item 5.

**Manager Planner Desktop — Task + Notes window:**
- "View the selected task's title and its full, date-ordered note timeline" — covered by item 7.
- "Mark the selected task Done" — covered by item 4.
- "Add a dated progress note" — covered by item 7.

**Manager Planner Desktop — Accountability Report window:**
- "View the promised-vs-delivered report across all projects" — covered by item 8.

**MDI window chrome (applies to all four child windows):**
- "Drag the title bar... to reposition" — covered by item 12.
- "Double-click the title bar, or the `PART_Max` button, to maximize/restore" — covered by item 12.
- "Drag the bottom-right resize grip... to resize" — covered by item 12.
- "Minimize... and close... both simply hide the window" — covered by item 12.

No capability from functional-spec.md's Capabilities section was excluded outright — every bullet (including the two menu/toolbar duplication notes) is either mapped into a backlog item above or explicitly named as a non-distinct alternate affordance of one.
