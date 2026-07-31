# Spec: Meeting recording and history

**Change:** meeting-recording-and-history
**Created:** 2026-07-31
**Status:** 🟡 Draft

## Overview

Adds the ability to record a meeting against a project and browse that
project's meeting history — the last remaining gap before `Meeting`
(already fully modeled: entity, enum, `PlanningDbContext` configuration)
has any service or UI surface in the rebuild. Two new `PlanningService`
methods, ported exactly from the legacy source, plus a new "Meetings"
section on the existing `ProjectDetail.razor` page. Resolves the
proposal's framing of `CQ-008` (already answered in substance by
`CQ-001`) and confirms, by direct source reads, the two nuances the
proposal flagged: the service method itself has no validation (the
legacy *caller* does), and the `MeetingType` dropdown shows literal enum
member names, not humanized text.

## Requirements

### Functional Requirements

1. **FR1 — `GetMeetingsForProjectAsync(projectId)`.** Ported exactly
   from `../manager-planner/src/ExecutivePlanning.Core/Services/
   PlanningService.cs:208-212`: `.Include(m => m.Participant)`, filtered
   by `ProjectId`, `OrderByDescending(m => m.MeetingDate)`.
2. **FR2 — `AddMeetingAsync(projectId, title, type, meetingDate,
   participantId)`.** Ported exactly from `PlanningService.cs:214-227`:
   constructs and saves a `Meeting` with the given fields — **no
   validation call of any kind**, matching the legacy service body
   exactly (unlike `AddProjectAsync`/`AddTaskAsync`/`AddObjectiveAsync`,
   which all call a `PlanningRules.Validate*` method first).
3. **FR3 — "Meetings" section on `ProjectDetail.razor`.** A record-
   meeting form (title text field, a `MeetingType` dropdown, a
   participant dropdown, a date picker) followed by a read-only,
   date-descending list of the project's meetings (title, type, date,
   participant name or a placeholder when unset) — the same page every
   prior backlog item has extended, not a new route.
4. **FR4 — `MeetingType` dropdown shows literal enum member names.**
   "VideoCall", "PhysicalMeeting", "PhoneCall" — confirmed by reading
   the legacy `MainWindowViewModel.cs:24`
   (`MeetingTypes = new ObservableCollection<MeetingType>(Enum.
   GetValues<MeetingType>())`, bound with no converter anywhere), so
   Avalonia's default binding renders the enum's own `.ToString()`. The
   rebuild reproduces this exactly rather than "improving" it into a
   humanized label.
5. **FR5 — Participant dropdown sourced from the existing
   `GetTeamMembersAsync()`.** The same team-member list already loaded
   for the task-assignee dropdown (`_teamMembers`) — no new query
   method. Includes a "— No participant —" option mapping to `null`,
   matching the existing Objective/Assignee dropdowns' "— Ungrouped —"/
   "— Unassigned —" pattern.
6. **FR6 — Page-level empty/whitespace-title rejection, matching the
   real legacy caller.** Reading
   `../manager-planner/src/ExecutivePlanning.Desktop/ViewModels/
   MainWindowViewModel.cs:159-174` directly: the *service* never
   validates the title, but the *caller* does —
   `if (string.IsNullOrWhiteSpace(NewMeetingTitle)) { StatusMessage =
   "Enter a meeting title."; return; }` — and trims
   (`NewMeetingTitle.Trim()`) before calling `AddMeetingAsync`. The
   rebuild's page-level handler reproduces both checks before calling
   the service, surfacing the rejection via the same `MudAlert`/error-
   message-field convention `AddObjectiveAsync`/`AddTaskAsync` already
   use on this page (not a thrown-and-caught exception, since the
   service throws none).
7. **FR7 — Reset-and-reload on success.** After a successful
   `AddMeetingAsync` call, the form's fields reset to their defaults,
   the error message clears, and the meeting list reloads — matching
   the existing `AddObjectiveAsync`/`AddTaskAsync` handlers' shape on
   this same page.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** Both new methods use
   `IDbContextFactory<PlanningDbContext>` like all eleven existing
   `PlanningService` methods.
2. **NFR2 — Scope discipline.** After this change, none of the
   following exist anywhere in the diff: linking a `ProgressNote` to a
   `Meeting` (backlog item BL-007, not yet built); wiring
   `WorkItem.DiscoveredInMeetingId` (a separate, already-logged
   non-blocking question, CQ-012); editing or deleting an existing
   `Meeting`; any tabbed/windowed reproduction of the legacy "Meetings &
   Notes" tab (ADR-0004 already settled MDI/tab chrome as re-interpreted
   web navigation, not reproduced).
3. **NFR3 — No humanizing of `MeetingType` values.** The dropdown and
   the history list both render the enum's literal member name — this
   is a deliberate fidelity decision (FR4), not an oversight to "fix"
   later.
4. **NFR4 — UTC-only dates, no per-feature local time.** The date
   picker's fallback-to-"now" behavior (when no date is explicitly
   picked) uses `DateTime.UtcNow`, not the legacy caller's literal
   `DateTimeOffset.Now` (a local-time value) — per this project's
   existing constraint ("No client-local-time concept exists anywhere
   in this app yet... don't introduce per-feature local-time formatting
   ad hoc"). This is a deliberate, documented deviation from the
   legacy's exact literal, not a missed nuance.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Submitting the record-meeting form with a non-empty title
   persists exactly one new `Meeting` row with `Title` trimmed, the
   selected `Type`, `MeetingDate`, `ProjectId`, and `ParticipantId` (or
   `null` if none selected) — confirmed by direct inspection of the
   persisted row.
3. **AC3** — Submitting the form with an empty or whitespace-only title
   creates no new `Meeting` row and shows an inline error message; no
   unhandled exception occurs (the service itself would not throw one
   either way, per FR2).
4. **AC4** — The meeting history list shows every meeting recorded for
   the project, ordered by `MeetingDate` descending.
5. **AC5** — Each history row shows the participant's `FullName` when
   `ParticipantId` is set, and a clear placeholder (not a blank cell)
   when it is null.
6. **AC6** — The `MeetingType` dropdown's three options render as
   exactly `VideoCall`, `PhysicalMeeting`, `PhoneCall` — no spaces, no
   humanized casing.
7. **AC7** — No `PlanningService` method exists beyond the eleven
   existing ones plus `GetMeetingsForProjectAsync`/`AddMeetingAsync`
   (thirteen total) anywhere in the diff.
8. **AC8** — No UI control anywhere links a note to a meeting, sets
   `WorkItem.DiscoveredInMeetingId`, or edits/deletes an existing
   `Meeting` row.

## Edge Cases

- **No participant selected.** `ParticipantId` persists as `null`; the
  history list shows a placeholder, not a blank or throwing cell (AC5).
- **Two meetings sharing the identical `MeetingDate`.** The legacy
  `GetMeetingsForProjectAsync` has no secondary sort key
  (`OrderByDescending(m => m.MeetingDate)` alone) — the rebuild carries
  this exact gap forward rather than inventing a tie-break, consistent
  with this project's existing treatment of the same shape in
  `GetProjectsAsync`/`GetTasksForProjectAsync` (assessed risk, tolerant
  comparison, not a defect to fix here).
- **No date explicitly picked.** Falls back to `DateTime.UtcNow` at
  submit time (NFR4) — not left null, since `Meeting.MeetingDate` is a
  non-nullable `DateTime`.
- **A project with zero recorded meetings.** The history list shows a
  "No meetings yet." placeholder, matching the existing "No objectives
  yet."/"No tasks yet." convention on this same page.

## Dependencies

- **Depends on:** `BL-001` (`Meeting.ProjectId` is a required FK to
  `Project`) — already built (`scaffold-blazor-solution`/
  `project-management`). `Meeting` does not depend on `Objective` or
  `WorkItem` (confirmed directly in rebuild-backlog.md's own
  "Depends on" line for this item).
- **Blocks:** `BL-007` (Progress notes — its canonical workflow sequences
  "Records a meeting" before "Adds a progress note," and
  `ProgressNote.MeetingId` needs real `Meeting` rows to link to).

## Notes

Both potential open questions the proposal could have raised are
resolved here by direct evidence, not left for a human decision: whether
meeting recording is in scope at all (`CQ-008`, resolved by the existing
`CQ-001` answer), and what text the `MeetingType` dropdown should show
(resolved by reading the legacy binding source directly — no converter,
so literal enum names).
