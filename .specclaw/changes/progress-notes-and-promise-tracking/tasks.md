# Tasks: Progress notes and promise tracking

**Change:** progress-notes-and-promise-tracking
**Created:** 2026-08-03
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) add `AddNoteAsync`/`GetNotesForTaskAsync`
to `PlanningService` and correct three validation message strings in
`PlanningRules`, (2) add a "Notes" section to `TaskRow` (history + add-note
form, wired to a new `Meetings` parameter) and pass `_meetings` down from
`ProjectDetail`, then verify end-to-end. No task adds an Accountability/
`Verdict` view, note editing/deletion, or a dedicated task-detail
page/route — those stay out of scope per spec.md NFR2/AC11/AC12.

## Tasks

### Wave 1 — Core business logic

- [x] `T1` — Add `AddNoteAsync`/`GetNotesForTaskAsync` to `PlanningService`; correct three `PlanningRules` message strings
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`, `src/ManagerPlanner.Core/Validation/PlanningRules.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs:234-261`, not the doc summary. `AddNoteAsync(int taskId, string text, int authorId, int? meetingId = null, bool isPromise = false, DateTime? promisedDate = null, DateTime? noteDate = null)`: open the factory-created context, call `PlanningRules.ValidateNoteText(text)` first, compute `var effectiveDate = noteDate ?? DateTime.UtcNow;`, call `PlanningRules.ValidateNoteDate(effectiveDate)`, then construct `new ProgressNote { WorkItemId = taskId, Text = text.Trim(), AuthorId = authorId, MeetingId = meetingId, IsPromise = isPromise, PromisedDate = promisedDate, NoteDate = effectiveDate }`, add it, `SaveChangesAsync()`, return it. `GetNotesForTaskAsync(int taskId)`: `db.ProgressNotes.Include(n => n.Author).Include(n => n.Meeting).Where(n => n.WorkItemId == taskId).OrderByDescending(n => n.NoteDate).ToListAsync()` — no `AsSplitQuery` (matches the legacy body exactly; only two single-reference includes, no collection includes). Both methods use `IDbContextFactory<PlanningDbContext>` like all thirteen existing methods. Then in `PlanningRules.cs`, correct exactly three message literals to match the real legacy `PlanningValidation.cs:53-76` verbatim — do not touch any validator's logic, only the string literals: `ValidateNoteText`'s overlong-text message becomes `$"The note is too long. Keep it under {MaxNoteText} characters."`; `ValidateNoteDate`'s backdated message becomes `$"That date is more than a month back. Notes can only be dated on or after {earliestAllowed:MMM dd, yyyy}."` (rename the existing local variable if needed so the format string reads naturally); its future-dated message becomes `"A note cannot be dated in the future."`. Confirm the corrected strings match fixtures `.specclaw/baseline/fixtures/GM-005.json`, `GM-006.json`, `GM-007.json` exactly, character for character.

### Wave 2 — Component + verification

- [x] `T2` — "Notes" section on `TaskRow`, `Meetings` parameter wiring, end-to-end verification
  - Files: `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor`, `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: In `TaskRow.razor`, add `[Parameter] public List<Meeting> Meetings { get; set; } = new();` and a private `List<ProgressNote>? _notes` field, loaded in a new `OnInitializedAsync` override: `_notes = await PlanningService.GetNotesForTaskAsync(WorkItem.Id);`. Add a fourth `<td>` cell ("Notes") to the existing row containing: (a) a read-only history list rendering `_notes` in the order `GetNotesForTaskAsync` returns them (newest `NoteDate` first, no client-side re-sort) — each entry shows the note text, `NoteDate` (`yyyy-MM-dd`, UTC — no local-time formatting per spec.md NFR3), the author's `FullName`, the linked meeting's `Title` when `MeetingId`/`Meeting` is set or a `"—"` placeholder when not, and a promise indicator (e.g. `"Promise due {PromisedDate:yyyy-MM-dd}"`) only when `IsPromise` is true; show `"No notes yet."` when `_notes` is empty, matching the existing "No objectives yet."/"No meetings yet." convention; (b) an add-note `EditForm`/`OnValidSubmit` with fields `_newNoteText` (multiline `MudTextField`), `_newNoteIsPromise` (`MudCheckBox`), `_newNotePromisedDate` (`MudDatePicker`, `Disabled="@(!_newNoteIsPromise)"`, initialized to `DateTime.UtcNow.AddDays(7)` — matches the real legacy `ExecutivePlanning.Desktop/ViewModels/MainWindowViewModel.cs:64` default, substituting UTC per NFR3), `_newNoteDate` (`MudDatePicker`, initialized to `DateTime.UtcNow.Date`), and `_newNoteMeetingId` (`MudSelect<int?>` populated from the `Meetings` parameter with a `"— No meeting —"` option mapping to `null`, matching the existing `"— Unassigned —"`/`"— No participant —"` dropdown convention) plus a `_noteErrorMessage` field rendered via `MudAlert` exactly like `AddObjectiveAsync`/`AddTaskAsync`'s existing error-message-field pattern on `ProjectDetail`. The submit handler: `var authorId = await PlanningService.GetCurrentManagerIdAsync();` then `try { await PlanningService.AddNoteAsync(WorkItem.Id, _newNoteText, authorId, _newNoteMeetingId, _newNoteIsPromise, _newNoteIsPromise ? _newNotePromisedDate : null, _newNoteDate); ... } catch (ManagerPlanner.Core.Validation.ValidationException ex) { _noteErrorMessage = ex.Message; }` — the `isPromise ? promisedDate : null` gating happens here, in the handler, not the service (spec.md FR4). On success: clear `_newNoteText`, reset `_newNoteIsPromise` to `false`, `_newNotePromisedDate` back to `DateTime.UtcNow.AddDays(7)`, `_newNoteDate` back to `DateTime.UtcNow.Date`, `_newNoteMeetingId` to `null`, clear `_noteErrorMessage`, and reload `_notes = await PlanningService.GetNotesForTaskAsync(WorkItem.Id);` — no callback to `ProjectDetail` (spec.md NFR4, matching `ChecklistTree.razor`'s existing row-owned-state pattern). In `ProjectDetail.razor`, add `Meetings="_meetings"` to both existing `<TaskRow WorkItem="task" StatusChanged="RefreshAsync" />` usages (the per-objective loop and the Ungrouped section) — no other change to `ProjectDetail.razor`.

    Verify manually: `dotnet build` at the solution root (AC1). Through the running app (per `.specclaw/context.md`'s established fallback — `form_input` for text fields, `read_page` immediately before every click, default straight to JS-dispatched `element.click()` via `javascript_tool` rather than attempting a real click first): submit a note with non-empty text, "is a promise" unchecked, a meeting selected, and confirm via direct DB inspection that exactly one new `ProgressNote` row persisted with the expected `Text`/`WorkItemId`/`AuthorId`/`NoteDate`/`MeetingId`, `IsPromise = false`, `PromisedDate = null` (AC2, AC3); check "is a promise", pick a promised date, submit, and confirm `IsPromise = true` with the matching `PromisedDate` (AC4); submit empty/whitespace-only text and confirm the exact message "The note is empty — type what was said before saving." renders, no new row persists, no unhandled exception (AC5); submit a 2001-character note and confirm the exact message "The note is too long. Keep it under 2000 characters." renders and no row persists, then confirm exactly 2000 characters is accepted (AC6); submit a note dated one day past the one-month-back boundary and confirm the exact message "That date is more than a month back. Notes can only be dated on or after {computed date}." with the correct date, then confirm a note dated exactly at the boundary is accepted (AC7); submit a note dated tomorrow and confirm the exact message "A note cannot be dated in the future." then confirm today's date is accepted (AC8); confirm a task's note history renders every note for that task ordered newest-`NoteDate`-first (AC9); confirm a history row with a linked meeting shows that meeting's title, a row with no linked meeting shows a placeholder, a promise-flagged row shows its promised date, and a non-promise row shows no promise indicator (AC10); confirm exactly fifteen `PlanningService` methods exist (AC11); confirm no UI control edits/deletes an existing note and no Accountability/Verdict view exists anywhere in the diff (AC12). Use a scratch console app or direct SQLite inspection to confirm persisted-row evidence per `.specclaw/context.md`'s documented preference over code-reading-only verification when a UI change ships without a test project to fall back on.

---

## Legend

- `[ ]` Pending
- `[~]` In Progress
- `[x]` Complete
- `[!]` Failed

**Task format:**
```
- [ ] `T<n>` — <title>
  - Files: <files to create/modify>
  - Estimate: small | medium | large
  - Depends: <task ids> (if any)
  - Notes: <additional context>
```
