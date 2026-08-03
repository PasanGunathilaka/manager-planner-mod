# Spec: Progress notes and promise tracking

**Change:** progress-notes-and-promise-tracking
**Created:** 2026-08-03
**Status:** 🟡 Draft

## Overview

Adds the ability to record a `ProgressNote` against a task and browse that
task's note history — the last remaining gap before `ProgressNote`
(already fully modeled: entity, `PlanningDbContext` configuration,
validation rules) has any service or UI surface in the rebuild. Two new
`PlanningService` methods, ported exactly from the legacy source, plus a
new "Notes" section on the existing `TaskRow.razor` component. This is
also the item that resolves rebuild-backlog.md's own flagged gap: the
three note-validation rejection messages already ported into this repo's
`PlanningRules` were paraphrased, not copied verbatim, from the legacy
text — this change corrects them to match exactly, confirmed against the
captured golden-master fixtures (`GM-005`, `GM-006`, `GM-007`).

## Requirements

### Functional Requirements

1. **FR1 — `AddNoteAsync(taskId, text, authorId, meetingId = null, isPromise = false, promisedDate = null, noteDate = null)`.**
   Ported exactly from
   `../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:234-254`:
   calls `PlanningRules.ValidateNoteText(text)`, computes
   `effectiveDate = noteDate ?? DateTime.UtcNow`, calls
   `PlanningRules.ValidateNoteDate(effectiveDate)`, then persists a
   `ProgressNote` with `Text` trimmed and `NoteDate = effectiveDate`.
2. **FR2 — `GetNotesForTaskAsync(taskId)`.** Ported exactly from
   `PlanningService.cs:256-261`: `.Include(n => n.Author)`,
   `.Include(n => n.Meeting)`, filtered by `WorkItemId`,
   `OrderByDescending(n => n.NoteDate)` — newest note first, not an
   ascending timeline.
3. **FR3 — "Notes" section on `TaskRow.razor`.** Each task row gains a
   note history list (newest first, per FR2's ordering) and an add-note
   form: a free-text box, a "This is a promise" checkbox, a promised-date
   picker, a note-date picker (defaulting to today), and a "link to
   meeting" dropdown sourced from the project's already-loaded meeting
   list (no new query — see Key Decision in design.md).
4. **FR4 — `IsPromise` gates `PromisedDate` persistence at the call
   site, not the service.** Both real legacy view models pass
   `promisedDate: isPromise ? promisedDate : null` — an unchecked
   "promise" box always saves `PromisedDate = null` even if a date was
   picked before the box was unchecked. The rebuild's add-note handler
   reproduces this exact gating before calling `AddNoteAsync`.
5. **FR5 — Promised-date picker defaults to +7 days from today**, matching
   the real legacy `ExecutivePlanning.Desktop` view model's
   `_newNotePromisedDate = DateTimeOffset.Now.AddDays(7)` initial value
   (read directly from
   `../manager-planner/src/ExecutivePlanning.Desktop/ViewModels/MainWindowViewModel.cs:64`) —
   using `DateTime.UtcNow.AddDays(7)` per this project's no-local-time
   constraint (see design.md Key Decisions).
6. **FR6 — Correct the three `PlanningRules` validation message strings**
   to match the legacy text verbatim, confirmed against the captured
   fixtures:
   - Overlong note text: `"The note is too long. Keep it under {MaxNoteText} characters."`
     (`GM-005`).
   - Backdated too far: `"That date is more than a month back. Notes can only be dated on or after {earliest:MMM dd, yyyy}."`
     (`GM-006`).
   - Future-dated: `"A note cannot be dated in the future."` (`GM-007`).
7. **FR7 — Meeting-link dropdown sourced from the project's meeting
   list.** Reuses `ProjectDetail.razor`'s already-loaded `_meetings` field
   (populated by `GetMeetingsForProjectAsync`, shipped in
   `meeting-recording-and-history`) — no new query. Includes a
   "— No meeting —" option mapping to `null`, matching the existing
   "— Ungrouped —"/"— Unassigned —"/"— No participant —" dropdown
   convention.
8. **FR8 — Reset-and-reload on success.** After a successful `AddNoteAsync`
   call, the form's fields reset to their defaults (text cleared,
   "is a promise" unchecked, promised-date back to +7 days, note-date back
   to today, meeting link back to "— No meeting —"), and the row's note
   list reloads via `GetNotesForTaskAsync` — matching the existing
   `AddObjectiveAsync`/`AddTaskAsync` handlers' reset-and-reload shape.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** Both new methods use
   `IDbContextFactory<PlanningDbContext>` like all thirteen existing
   `PlanningService` methods.
2. **NFR2 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: the Accountability report or any `Verdict`
   computation (backlog item BL-008, not yet built); editing or deleting
   an existing `ProgressNote`; a dedicated task-detail page/route; task
   selection driving which notes are shown (every task row already shows
   its own notes inline — there is no separate "select a task" step to
   reproduce).
3. **NFR3 — UTC-only dates, no per-feature local time.** The note-date
   picker's default (today) and the promised-date picker's default
   (+7 days) both use `DateTime.UtcNow`, not the legacy view models'
   `DateTimeOffset.Now` — per this project's existing constraint ("No
   client-local-time concept exists anywhere in this app yet... don't
   introduce per-feature local-time formatting ad hoc").
4. **NFR4 — Row-owned state, no page-level refresh callback.** Following
   the precedent set by `ChecklistTree.razor` ("checklist toggles update
   local component state only — no `EventCallback`, no
   `ProjectDetail.RefreshAsync`" — nothing else on the page derives from
   note state), `TaskRow` loads and refreshes its own notes locally; it
   does not raise a `NotesChanged` callback or call into
   `ProjectDetail.RefreshAsync`.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Submitting the add-note form with non-empty text persists
   exactly one new `ProgressNote` row with `Text` trimmed, `WorkItemId`
   set to the row's task, `AuthorId` set via
   `GetCurrentManagerIdAsync()`, `NoteDate` equal to the picked note date,
   and `MeetingId` equal to the selected meeting (or `null` if
   "— No meeting —" is selected) — confirmed by direct inspection of the
   persisted row.
3. **AC3** — Submitting with the "is a promise" checkbox **unchecked**
   persists `IsPromise = false` and `PromisedDate = null`, even if a date
   remains selected in the (now-irrelevant) promised-date picker.
4. **AC4** — Submitting with the checkbox **checked** persists
   `IsPromise = true` and `PromisedDate` equal to the picked promised
   date.
5. **AC5** — Submitting with empty/whitespace-only note text shows the
   exact message `"The note is empty — type what was said before
   saving."`, creates no new `ProgressNote` row, and raises no unhandled
   exception.
6. **AC6** — Submitting note text over 2000 characters shows the exact
   message `"The note is too long. Keep it under 2000 characters."` and
   creates no new row; exactly 2000 characters is accepted (matching
   `GM-005`'s captured boundary).
7. **AC7** — Submitting a note dated more than one month before today
   shows the exact message `"That date is more than a month back. Notes
   can only be dated on or after {earliest:MMM dd, yyyy}."` with the
   correct computed `earliest` date; a note dated exactly one month back
   is accepted (matching `GM-006`'s captured boundary).
8. **AC8** — Submitting a note dated after today shows the exact message
   `"A note cannot be dated in the future."`; a note dated today is
   accepted (matching `GM-007`'s captured boundary).
9. **AC9** — The note history list on a task with existing notes shows
   every note for that task, ordered by `NoteDate` descending (newest
   first).
10. **AC10** — Each history row shows the linked meeting's title when
    `MeetingId` is set, and a clear placeholder (not a blank cell) when it
    is null; shows a promise indicator (including the promised date) when
    `IsPromise` is true, and no promise indicator when false.
11. **AC11** — No `PlanningService` method exists beyond the thirteen
    existing ones plus `AddNoteAsync`/`GetNotesForTaskAsync` (fifteen
    total) anywhere in the diff.
12. **AC12** — No UI control anywhere edits or deletes an existing
    `ProgressNote`, and no Accountability/`Verdict` view exists anywhere
    in the diff.

## Edge Cases

- **A task with zero recorded notes.** The history list shows a
  "No notes yet." placeholder, matching the existing "No objectives
  yet."/"No tasks yet."/"No meetings yet." convention.
- **Two notes sharing the identical `NoteDate`.** The legacy
  `GetNotesForTaskAsync` has no secondary sort key
  (`OrderByDescending(n => n.NoteDate)` alone) — the rebuild carries this
  exact gap forward rather than inventing a tie-break, consistent with
  this project's existing treatment of the same shape in
  `GetProjectsAsync`/`GetMeetingsForProjectAsync`.
- **No meeting selected.** `MeetingId` persists as `null`; the history row
  shows a placeholder, not a blank or throwing cell (AC10).
- **Checkbox checked, then unchecked before submit.** `PromisedDate`
  persists as `null` regardless of what remains selected in the
  (disabled) promised-date picker — the checkbox state at submit time is
  authoritative (AC3, FR4).
- **Exact boundary values** (2000-char text, exactly-one-month-back date,
  today's date) — all three are accepted, not rejected; only values
  strictly beyond each boundary are rejected (AC6, AC7, AC8, matching
  `GM-005`/`GM-006`/`GM-007` exactly).

## Dependencies

- **Depends on:** `BL-003` (`ProgressNote.WorkItemId` is a required FK to
  `WorkItem` — already built, `task-management`) and `BL-006`
  (`ProgressNote.MeetingId` is optional, but the meeting-link dropdown
  needs real `Meeting` rows to select from — already built,
  `meeting-recording-and-history`).
- **Blocks:** `BL-008` (Accountability reporting — its `Verdict`
  computation reads each task's latest `ProgressNote` promise; nothing can
  be computed until notes exist to read).

## Notes

Both open questions the proposal raised are resolved here, per the
proposal's own recommendation, not left open:

1. **The three validation message strings are corrected to the legacy
   text verbatim** (FR6) — confirmed by the captured `GM-005`/`GM-006`/
   `GM-007` fixtures, not just the proposal's own reading of the source.
2. **The meeting-link dropdown reuses `ProjectDetail.razor`'s existing
   `_meetings` field**, passed down to `TaskRow` as a new parameter — no
   new service call, consistent with how the participant dropdown already
   reuses `_teamMembers` (`meeting-recording-and-history` Key Decision 3).
