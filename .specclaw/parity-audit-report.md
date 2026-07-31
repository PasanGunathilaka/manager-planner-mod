# Parity Audit Report
Generated: 2026-07-31T00:00:00Z
Golden master: .specclaw/golden-master-legacy.json
Matrix: .specclaw/matrix-inputs.json

## Summary
- Total cases evaluated: 181
- Matches: 80
- Discrepancies: 101
- Match percentage: 44.20%
- Status: BLOCKED

## Method used
Re-ran the existing disposable console-app harness at `.specclaw/parity-harness/`
(`ParityHarness.csproj`, referencing `src/ManagerPlanner.Core/ManagerPlanner.Core.csproj` via
`<ProjectReference>`), updated for the one relevant change since the prior run: the
`nested-checklist-items-and-grid-status-badges` change added `PlanningService.
ToggleChecklistItemAsync`, previously recorded as not-implemented. Verified first (via `git diff`
against the commit the prior audit ran against) that no other file relevant to this matrix —
`PlanningValidation.cs`, `Reports.cs`, `PlanningDbContext.cs`, or any other `PlanningService` method
— changed in between, so every other case's result is carried forward unchanged from the prior
audit rather than re-executed redundantly. Added real dispatch for the three
`ToggleChecklistItemAsync` cases (`MOD03-C042/C043/C044`) to `Program.cs`: `C042` calls it with a
nonexistent `itemId` directly (no arrange needed); `C043`/`C044` seed a `WorkItem` via the real
`AddTaskAsync` plus a directly-inserted `ChecklistItem` (no `AddChecklistItemAsync` exists to do
this via the service, matching the prior audit's own established pattern for other DB-arranged
cases), then call the real `ToggleChecklistItemAsync` and reload from a fresh context to inspect
persisted state. Build and run both succeeded cleanly (`dotnet build` / `dotnet run`, 0 warnings,
0 errors, 181/181 case results written to `.specclaw/parity-harness/modern-results.json`).

All three newly-dispatched cases matched the golden master exactly:
- `MOD03-C042`: `InvalidOperationException` — `"Checklist item 999999 not found."`, byte-identical
  to the golden master (unsurprising, since the ported method's not-found branch is a verbatim copy
  of the legacy source).
- `MOD03-C043`: `{"IsDone":true,"CompletedUtcIsNull":false}`, exact match.
- `MOD03-C044`: `{"IsDone":false,"CompletedUtcIsNull":true}`, exact match — confirms the symmetric
  clearing-to-null half of the same line.

## Discrepancies
(sorted by module, then case_id ascending; placeholder shorthand like `<121 'a' chars>` is carried
over verbatim from matrix-inputs.json for readability — it was expanded literally when constructing
the actual harness inputs)

| Case ID | Input | Legacy Output | Modern Output | Delta |
|---|---|---|---|---|
| MOD01-C001 | `{"name": null}` | exception: "Project name cannot be empty." | exception: "Project name is required." | message differs |
| MOD01-C002 | `{"name": ""}` | exception: "Project name cannot be empty." | exception: "Project name is required." | message differs |
| MOD01-C003 | `{"name": "   "}` | exception: "Project name cannot be empty." | exception: "Project name is required." | message differs |
| MOD01-C007 | `{"name": "<121 'a' chars>"}` | exception: "Project name is too long (121). Keep it to 120 characters." | exception: "Project name cannot exceed 120 characters." | message differs (modern also drops the interpolated actual length) |
| MOD01-C009 | `{"title": null}` | exception: "Task title cannot be empty." | exception: "Task title is required." | message differs |
| MOD01-C010 | `{"title": ""}` | exception: "Task title cannot be empty." | exception: "Task title is required." | message differs |
| MOD01-C011 | `{"title": "   "}` | exception: "Task title cannot be empty." | exception: "Task title is required." | message differs |
| MOD01-C015 | `{"title": "<121 'a' chars>"}` | exception: "Task title is too long (121). Keep it to 120 characters." | exception: "Task title cannot exceed 120 characters." | message differs |
| MOD01-C017 | `{"title": null}` (Objective) | exception: "Objective title cannot be empty." | exception: "Objective title is required." | message differs |
| MOD01-C018 | `{"title": ""}` (Objective) | exception: "Objective title cannot be empty." | exception: "Objective title is required." | message differs |
| MOD01-C019 | `{"title": "   "}` (Objective) | exception: "Objective title cannot be empty." | exception: "Objective title is required." | message differs |
| MOD01-C023 | `{"title": "<151 'a' chars>"}` | exception: "Objective title is too long (151). Keep it to 150 characters." | exception: "Objective title cannot exceed 150 characters." | message differs |
| MOD01-C025 | `{"label": null}` | exception: "Checklist item cannot be empty." | exception: "Checklist label is required." | message differs |
| MOD01-C026 | `{"label": ""}` | exception: "Checklist item cannot be empty." | exception: "Checklist label is required." | message differs |
| MOD01-C027 | `{"label": "   "}` | exception: "Checklist item cannot be empty." | exception: "Checklist label is required." | message differs |
| MOD01-C031 | `{"label": "<301 'a' chars>"}` | exception: "Checklist item is too long. Keep it to 300 characters." | exception: "Checklist label cannot exceed 300 characters." | message differs |
| MOD01-C039 | `{"text": "<2001 'a' chars>"}` | exception: "The note is too long. Keep it under 2000 characters." | exception: "The note is too long — it cannot exceed 2000 characters." | message differs |
| MOD01-C041 | `{"noteDateUtc": "0001-01-01T00:00:00Z", "nowUtc": null}` | exception: "That date is more than a month back. Notes can only be dated on or after Jun 30, 2026." | exception: "The note date cannot be more than 1 month(s) in the past." | message differs (legacy embeds the real earliest date, modern does not) |
| MOD01-C043 | `{"noteDateUtc": "2026-06-29T00:00:00Z", "nowUtc": "2026-07-30T00:00:00Z"}` | exception: "That date is more than a month back. Notes can only be dated on or after Jun 30, 2026." | exception: "The note date cannot be more than 1 month(s) in the past." | message differs |
| MOD01-C045 | `{"noteDateUtc": "2026-07-31T00:00:00Z", "nowUtc": "2026-07-30T00:00:00Z"}` | exception: "A note cannot be dated in the future." | exception: "The note date cannot be in the future." | message differs |
| MOD01-C047 | `{"noteDateUtc": "2024-02-28T00:00:00Z", "nowUtc": "2024-03-31T00:00:00Z"}` | exception: "That date is more than a month back. Notes can only be dated on or after Feb 29, 2024." | exception: "The note date cannot be more than 1 month(s) in the past." | message differs |
| MOD02-C001 | `{"PromiseKept":true,"PromiseBroken":true,"IsOverdue":true,"LatestPromisedDate":"2026-12-01"}` | "Kept promise" | (not implemented in modern codebase) | not implemented |
| MOD02-C002 | `{"PromiseKept":true,"PromiseBroken":false,"IsOverdue":false,"LatestPromisedDate":null}` | "Kept promise" | (not implemented in modern codebase) | not implemented |
| MOD02-C003 | `{"PromiseKept":false,"PromiseBroken":true,"IsOverdue":true,"LatestPromisedDate":"2026-12-01"}` | "BROKE promise" | (not implemented in modern codebase) | not implemented |
| MOD02-C004 | `{"PromiseKept":false,"PromiseBroken":true,"IsOverdue":false,"LatestPromisedDate":null}` | "BROKE promise" | (not implemented in modern codebase) | not implemented |
| MOD02-C005 | `{"PromiseKept":false,"PromiseBroken":false,"IsOverdue":true,"LatestPromisedDate":"2026-12-01"}` | "Overdue (no promise)" | (not implemented in modern codebase) | not implemented |
| MOD02-C006 | `{"PromiseKept":false,"PromiseBroken":false,"IsOverdue":true,"LatestPromisedDate":null}` | "Overdue (no promise)" | (not implemented in modern codebase) | not implemented |
| MOD02-C007 | `{"PromiseKept":false,"PromiseBroken":false,"IsOverdue":false,"LatestPromisedDate":"2026-12-01"}` | "Promise pending" | (not implemented in modern codebase) | not implemented |
| MOD02-C008 | `{"PromiseKept":false,"PromiseBroken":false,"IsOverdue":false,"LatestPromisedDate":null}` | "On track" | (not implemented in modern codebase) | not implemented |
| MOD03-C001 | `HasAnyData {}` | false | (not implemented in modern codebase) | not implemented |
| MOD03-C002 | `HasAnyData {"usersPresent":1}` | true | (not implemented in modern codebase) | not implemented |
| MOD03-C003 | `LoadSampleDataIfEmpty {}` | `{"Returned":true,"UsersAfter":6}` | (not implemented in modern codebase) | not implemented |
| MOD03-C004 | `LoadSampleDataIfEmpty {"usersPresent":1}` | `{"Returned":false,"UsersAfter":1}` | (not implemented in modern codebase) | not implemented |
| MOD03-C005 | `ResetSampleData {"priorState":"seeded then extra task"}` | `{"Users":6,"Projects":3,"ExtraTaskGone":true}` | (not implemented in modern codebase) | not implemented |
| MOD03-C006 | `ResetSampleData {"priorState":"empty database"}` | `{"Users":6,"Projects":3}` | (not implemented in modern codebase) | not implemented |
| MOD03-C007 | `GetUsersAsync {}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C008 | `GetUsersAsync {"users":[tie on FullName]}` | `[{"Id":1,...},{"Id":2,...}]` | (not implemented in modern codebase) | not implemented |
| MOD03-C011 | `AddUserAsync {"fullName":"Valid Name","email":"valid@test.local","role":"Manager"}` | `{"Id":1,"FullName":"Valid Name",...}` | (not implemented in modern codebase) | not implemented |
| MOD03-C012 | `AddUserAsync {"fullName":null,"email":"nullname@test.local"}` | exception: DbUpdateException (NOT NULL Users.FullName) | (not implemented in modern codebase) | not implemented |
| MOD03-C013 | `AddUserAsync {"email":"dup@test.local (duplicate)"}` | exception: DbUpdateException (UNIQUE Users.Email) | (not implemented in modern codebase) | not implemented |
| MOD03-C014 | `AddUserAsync {"email":"<201-char>@test.local"}` | `{"EmailLength":201,"PersistedLength":201,"PersistedEqualsInput":true}` | (not implemented in modern codebase) | not implemented |
| MOD03-C020 | `DeleteProjectAsync {"projectId":999999}` | "no-op (project not found), no exception" | (not implemented in modern codebase) | not implemented |
| MOD03-C021 | `DeleteProjectAsync {"projectSubtree": full tree}` | all counts 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C022 | `DeleteTaskAsync {"taskId":999999}` | "no-op (task not found), no exception" | (not implemented in modern codebase) | not implemented |
| MOD03-C023 | `DeleteTaskAsync {"taskSubtree": checklist+note+status+owner}` | all counts 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C024 | `GetTasksForProjectAsync {"tasks":["HasDeadline","NullDeadlineFirst","NullDeadlineSecond"]}` | `["HasDeadline","NullDeadlineFirst","NullDeadlineSecond"]` | (not implemented in modern codebase) | not implemented |
| MOD03-C025 | `GetTasksForProjectAsync {"projectId":999999}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C026 | `GetTaskAsync {"taskId":999999}` | `{"IsNull":true}` | (not implemented in modern codebase) | not implemented |
| MOD03-C027 | `GetTaskAsync {"taskId":"<valid>"}` | `{"ProjectId":1,"AssigneeId":2,...}` | (not implemented in modern codebase) | not implemented |
| MOD03-C032 | `GetObjectivesForProjectAsync {"projectId":999999}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C033 | `GetObjectivesForProjectAsync {"objectives":["First","Second"]}` | `[{"Title":"First","SortOrder":0},{"Title":"Second","SortOrder":1}]` | (not implemented in modern codebase) | not implemented |
| MOD03-C039 | `AddChecklistItemAsync {"existingItemsForTask":0}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C040 | `AddChecklistItemAsync {"parentId":"<valid>"}` | `{"ParentId":1,"ExpectedParentId":1}` | (not implemented in modern codebase) | not implemented |
| MOD03-C041 | `AddChecklistItemAsync {"parentId":999999}` | exception: DbUpdateException (FK constraint) | (not implemented in modern codebase) | not implemented |
| MOD03-C045 | `SetOwnersAsync {"firstCall":[member,manager],"secondCall":[member]}` | `[2]` | (not implemented in modern codebase) | not implemented |
| MOD03-C046 | `SetOwnersAsync {"userIds":[member,member,manager]}` | `{"Count":2,"Owners":[1,2]}` | (not implemented in modern codebase) | not implemented |
| MOD03-C047 | `SetOwnersAsync {"userIds":[]}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C056 | `GetMeetingsForProjectAsync {"meetings":["First","Second"],"sameMeetingDate":true}` | `["First (Alpha)","Second (Beta)"]` | (not implemented in modern codebase) | not implemented |
| MOD03-C057 | `GetMeetingsForProjectAsync {"projectId":999999}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C058 | `AddMeetingAsync {"title":""}` | `{"TitleIsEmpty":true}` | (not implemented in modern codebase) | not implemented |
| MOD03-C059 | `AddMeetingAsync {"participantId":999999}` | exception: DbUpdateException (FK constraint) | (not implemented in modern codebase) | not implemented |
| MOD03-C060 | `AddNoteAsync {"noteDate":null}` | `{"NoteDateIsToday":true}` | (not implemented in modern codebase) | not implemented |
| MOD03-C061 | `AddNoteAsync {"text":"   "}` | exception: "The note is empty — type what was said before saving." | (not implemented in modern codebase) | not implemented |
| MOD03-C062 | `AddNoteAsync {"noteDate":"<UtcNow - 2mo>"}` | exception: "That date is more than a month back. Notes can only be dated on or after Jun 30, 2026." | (not implemented in modern codebase) | not implemented |
| MOD03-C063 | `AddNoteAsync {"noteDate":"<UtcNow + 2d>"}` | exception: "A note cannot be dated in the future." | (not implemented in modern codebase) | not implemented |
| MOD03-C064 | `AddNoteAsync {"isPromise":false,"promisedDate":"<UtcNow + 5d>"}` | `{"IsPromise":false,"PromisedDateIsSet":true}` | (not implemented in modern codebase) | not implemented |
| MOD03-C065 | `AddNoteAsync {"authorId":999999}` | exception: DbUpdateException (FK constraint) | (not implemented in modern codebase) | not implemented |
| MOD03-C066 | `GetNotesForTaskAsync {"notes":["First","Second"],"sameNoteDate":true}` | `["First (Alpha)","Second (Beta)"]` | (not implemented in modern codebase) | not implemented |
| MOD03-C067 | `GetNotesForTaskAsync {"taskId":999999}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C068 | `GetAccountabilityReportAsync {"task":"no promise, no deadline, not Done"}` | `{"Verdict":"On track",...}` | (not implemented in modern codebase) | not implemented |
| MOD03-C069 | `GetAccountabilityReportAsync {"task":"no promise, deadline -2d, not Done"}` | `{"IsOverdue":true,"Verdict":"Overdue (no promise)"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C070 | `GetAccountabilityReportAsync {"task":"no deadline, pending promise +5d"}` | `{"Verdict":"Promise pending"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C071 | `GetAccountabilityReportAsync {"task":"Done, deadline -3d"}` | `{"IsOverdue":false,"Verdict":"On track"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C072 | `GetAccountabilityReportAsync {"task":"Done, CompletedUtc==PromisedDate"}` | `{"PromiseKept":true,"Verdict":"Kept promise"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C073 | `GetAccountabilityReportAsync {"task":"Done, CompletedUtc +1d vs PromisedDate"}` | `{"PromiseBroken":true,"Verdict":"BROKE promise"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C074 | `GetAccountabilityReportAsync {"task":"Done, CompletedUtc -1d vs PromisedDate"}` | `{"PromiseKept":true,"Verdict":"Kept promise"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C075 | `GetAccountabilityReportAsync {"task":"not Done, PromisedDate==today"}` | `{"PromiseBroken":false,"Verdict":"Promise pending"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C076 | `GetAccountabilityReportAsync {"task":"not Done, PromisedDate -1d"}` | `{"PromiseBroken":true,"Verdict":"BROKE promise"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C077 | `GetAccountabilityReportAsync {"task":"not Done, PromisedDate +3d"}` | `{"PromiseBroken":false,"Verdict":"Promise pending"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C078 | `GetAccountabilityReportAsync {"notes":["older would-be-broken","newer pending"]}` | `{"LatestPromisedDate":"...","PromiseBroken":false}` | (not implemented in modern codebase) | not implemented |
| MOD03-C079 | `GetAccountabilityReportAsync {"note":"IsPromise=false, PromisedDate set"}` | `{"LatestPromisedDateIsNull":true,"Verdict":"On track"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C080 | `GetAccountabilityReportAsync {"note":"IsPromise=true, PromisedDate=null"}` | `{"LatestPromisedDateIsNull":true,"Verdict":"On track"}` | (not implemented in modern codebase) | not implemented |
| MOD03-C081 | `GetAccountabilityReportAsync {"task":"AssigneeId null"}` | "(unassigned)" | (not implemented in modern codebase) | not implemented |
| MOD03-C082 | `GetAccountabilityReportAsync {"tasks":["Alpha","Beta"], full tie}` | `[Alpha,Beta]` (unspecified tie order) | (not implemented in modern codebase) | not implemented |
| MOD03-C083 | `GetAccountabilityReportAsync {"projectId":999999}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD03-C084 | `GetAccountabilityForAllProjectsAsync {"projects":["Zulu Co","Alpha Co"], tie}` | `["Alpha Co","Zulu Co"]` | (not implemented in modern codebase) | not implemented |
| MOD03-C085 | `GetAccountabilityForAllProjectsAsync {}` | 0 | (not implemented in modern codebase) | not implemented |
| MOD05-C001 | `Seed {}` (Users count) | 6 | (not implemented in modern codebase) | not implemented |
| MOD05-C002 | `Seed {}` (Projects count) | 3 | (not implemented in modern codebase) | not implemented |
| MOD05-C003 | `Seed {}` (WorkItems count) | 10 | (not implemented in modern codebase) | not implemented |
| MOD05-C004 | `Seed {}` (Objectives count) | 6 | (not implemented in modern codebase) | not implemented |
| MOD05-C005 | `Seed {}` (Meetings count) | 1 | (not implemented in modern codebase) | not implemented |
| MOD05-C006 | `Seed {}` (ProgressNotes count) | 6 | (not implemented in modern codebase) | not implemented |
| MOD05-C007 | `Seed {}` (StatusChanges count) | 2 | (not implemented in modern codebase) | not implemented |
| MOD05-C008 | `Seed {}` (ChecklistItems count) | 17 | (not implemented in modern codebase) | not implemented |
| MOD05-C009 | `Seed {}` (TaskOwners count) | 12 | (not implemented in modern codebase) | not implemented |
| MOD05-C010 | `Seed {}` (IsDiscovered count) | 1 | (not implemented in modern codebase) | not implemented |
| MOD05-C011 | `Seed {}` (nested ChecklistItems count) | 7 | (not implemented in modern codebase) | not implemented |
| MOD05-C012 | `SeedIfEmpty {"priorState":"one unrelated user"}` | `{"UsersAfter":1,"ProjectsAfter":0}` | (not implemented in modern codebase) | not implemented |
| MOD05-C013 | `ResetToSampleData {"priorState":"seeded + extra task"}` | `{"Users":6,"Projects":3,"ExtraTaskGone":true}` | (not implemented in modern codebase) | not implemented |
| MOD05-C014 | `ResetToSampleData {"priorState":"freshly seeded"}` | `{"Users":6,"Projects":3}` | (not implemented in modern codebase) | not implemented |

## Golden Master Gaps
None. Verified programmatically: the set of `case_id` values in `matrix-inputs.json` and
`golden-master-legacy.json` are identical (181 in each, same 48/17/90/12/14 per-module split,
`diff` of the two sorted id lists produced zero output).

## Notes

**What changed since the prior run:** the `nested-checklist-items-and-grid-status-badges` change
added `PlanningService.ToggleChecklistItemAsync`, ported verbatim from the legacy source. All three
of its matrix cases (`MOD03-C042/C043/C044`) now match the golden master exactly — the not-found
exception message, the `IsDone`/`CompletedUtc`-set-on-true behavior, and the symmetric
`CompletedUtc`-cleared-on-false behavior. This moves 3 cases from "not implemented" to "match":
77 → 80 matches, 42.54% → 44.20%. Nothing else in the modern codebase changed (confirmed via `git
diff` against the prior audit's commit for `PlanningValidation.cs`, `Reports.cs`,
`PlanningDbContext.cs`, and the rest of `PlanningService.cs` — zero differences besides the new
method's own addition), so every other case's result below is carried forward unchanged, not
guessed or re-derived.

**Module-level results:**
- **MOD01 (`PlanningRules` / legacy `PlanningValidation`)** — 27/48 match, 21/48 discrepancies.
  Every boundary/length threshold (120/120/150/300/2000 chars, 1-month backdate window) is an
  exact behavioral port — no boundary-crossing case (throws-vs-doesn't) disagreed. The 21
  discrepancies are **all exception message text mismatches**, not logic bugs: the modern
  `ValidationException` messages were rewritten wholesale (e.g. "Project name cannot be empty."
  → "Project name is required."; "...is too long (121). Keep it to 120 characters." → "...cannot
  exceed 120 characters." with the actual violating length no longer interpolated at all;
  `ValidateNoteDate`'s too-old message no longer embeds the computed earliest-allowed date). This
  is a systemic, codebase-wide rewording across all 5 validators plus the date rule — not an
  isolated defect in one method. Unchanged since the prior audit.
- **MOD01-C046**: golden master's captured `output` field is
  `"no exception (validation passed); earliest=2024-02-29"` — the `; earliest=...` suffix is
  extraction-tooling commentary about the internal `AddMonths(-1)` leap-day-clamp calculation, not
  a literal value `ValidateNoteDate` (a `void` method) actually returns. Modern code also throws no
  exception here. Treated as a **match** on the substantive behavior (both non-throwing); the real
  test of the leap-day clamp value is MOD01-C047, which **does** show a message mismatch (counted
  above) but confirms modern also computes the clamped earliest date as Feb 29 2024 (the exception
  fires at exactly the right boundary, only the message wording differs).
- **MOD02 (`Reports.cs`)** — 9/17 match, 8/17 discrepancies. `ProjectSummary.PercentComplete` is a
  byte-for-byte exact port (confirmed for every rounding case: ordinary rounding, both
  round-half-to-even midpoint ties in C013/C014, IEEE-754 negative zero in C015, and the
  double-promotion overflow-avoidance in C016/C017 — all matched exactly). However, the
  `AccountabilityRow` class (with its `Verdict` computed property) **does not exist anywhere** in
  `src/ManagerPlanner.Core` (confirmed by project-wide grep for `AccountabilityRow`, `Verdict`,
  `PromiseKept`, `PromiseBroken`, `IsOverdue` — zero matches) — all 8 `Verdict`-precedence cases are
  not implemented. Unchanged since the prior audit.
- **MOD03 (`PlanningService`)** — 32/90 match, 58/90 discrepancies (up from 29/61 — the 3
  `ToggleChecklistItemAsync` cases now match). The modern `PlanningService` has **11 public
  methods** total (`GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`,
  `GetCurrentManagerIdAsync`, `AddObjectiveAsync`, `GetPlannerForProjectAsync`, `AddTaskAsync`,
  `GetTeamMembersAsync`, `GetUngroupedTasksForProjectAsync`, `ChangeStatusAsync`, and now
  `ToggleChecklistItemAsync`) — confirmed by reading the file directly. Still missing, confirmed by
  `grep` for every other legacy method name across the whole project (zero matches for all of
  them): `GetUsersAsync`, `AddUserAsync`, `DeleteProjectAsync`, `DeleteTaskAsync`,
  `GetTasksForProjectAsync`, `GetTaskAsync`, `GetObjectivesForProjectAsync`,
  `AddChecklistItemAsync`, `SetOwnersAsync`, `GetMeetingsForProjectAsync`, `AddMeetingAsync`,
  `AddNoteAsync`, `GetNotesForTaskAsync`, `GetAccountabilityReportAsync`, and
  `GetAccountabilityForAllProjectsAsync` — meaning Meetings, ProgressNotes, new-checklist-item
  creation, TaskOwner management, and the entire Accountability reporting feature still have no
  service-layer entry point in the modern codebase, even though their underlying domain entities
  (`Meeting`, `ProgressNote`, `ChecklistItem`, `TaskOwner`, `StatusChange`) and DB schema all exist.
  Every one of the 32 cases whose target method **does** exist matched the golden master exactly
  (list ordering including unspecified-tie insertion order, FK-violation exception type/message/
  inner-message, trimming, SortOrder counting, status-history bookkeeping, the
  `GetProjectSummaryAsync` aggregate counts, and now `ToggleChecklistItemAsync`'s not-found
  exception and completion-timestamp stamp/clear behavior) — the ported subset remains fully
  faithful.
- **MOD03-C029** (`AddTaskAsync` with `isDiscovered=true`): matched the golden master's output
  values, but only because the modern `AddTaskAsync` signature has **no `discoveredInMeetingId`
  parameter at all** (legacy: `AddTaskAsync(..., bool isDiscovered, int? discoveredInMeetingId)`;
  modern: `AddTaskAsync(int projectId, string title, string? description, int? assigneeId,
  DateTime? deadline, bool isDiscovered = false, int? objectiveId = null)`), so
  `DiscoveredInMeetingId` is unconditionally null regardless of input — coincidentally identical to
  this specific case's expected output. This is a real, narrower API surface than the legacy
  method (there is currently no way to link a discovered task back to the meeting it was discovered
  in through this service), flagged here for visibility even though it did not register as a
  case-level discrepancy. Unchanged since the prior audit.
- **MOD04 (`PlanningDbContext`)** — **12/12 match, 0 discrepancies.** Every Cascade/SetNull/Restrict
  rule reproduced exactly, including the two subtle EF Core behavioral nuances the matrix
  specifically targets: the client-side `InvalidOperationException` for required-FK Restrict
  violations firing *before* any SQL is issued (MOD04-C006/C007/C008, exact message text matched
  verbatim including entity-type names), and the same-context-vs-fresh-context split behavior of
  the optional self-referencing `ChecklistItem.Parent` Restrict rule (MOD04-C009 succeeds silently
  via client-side fixup; MOD04-C012 throws the real `DbUpdateException`/`SqliteException` FK
  message) — both halves reproduced exactly. The relational schema migration is fully faithful.
  Unchanged since the prior audit (this change touched no schema/`DbContext` code).
- **MOD05 (`DbSeeder`)** — 0/14 match, 14/14 discrepancies. Confirmed (as anticipated by the task
  brief) that no `DbSeeder` class, nor any seed-related method, exists anywhere in
  `src/ManagerPlanner.Core`. Unchanged since the prior audit.
- **MOD04-C010 arrangement caveat**: this case's golden output includes a specific numeric
  `UsersCountAfter: 2`. The harness's arrangement (Manager + Member + one TaskOwner-only user, then
  removing the TaskOwner-only user) was inferred from the case's own rationale text and the
  `.specclaw/baseline/harness/Arrange.cs` precedent's "Manager+Member+Project" baseline pattern
  (not verified against the actual legacy test source, which was not opened for this audit) — it
  happened to reproduce the golden count exactly, but flagging the inference for transparency.
  Unchanged since the prior audit.

**Overall:** the ported subset of `PlanningRules`' boundary logic, `PlanningDbContext`'s relational
integrity rules, and now `ToggleChecklistItemAsync` is exact. The two categories of failure remain
unchanged in kind, only smaller in the MOD03 count: (1) a wholesale rewording of every validation
exception message (MOD01, 21 cases) and (2) a still-substantial missing surface area of
`PlanningService` methods and the `AccountabilityRow` type (MOD02 + MOD03, 66 cases combined) plus
the entirely-absent `DbSeeder` (MOD05, 14 cases) — 101 discrepancies total, still forcing `BLOCKED`.
