# Proposal: Progress notes and promise tracking

**Created:** 2026-08-03
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

Nothing in the rebuild can record a `ProgressNote` yet. Per domain-model.md,
`ProgressNote` is "the heart of the accountability feature: the Manager can
flag that the team member *promised* something by a certain date, then
later cross-check promise vs delivery" — without this item, that promise
history can never be captured, and rebuild-backlog item 8 (Accountability
reporting) has nothing to read: its `Verdict` computation is derived
entirely from a task's *latest* `ProgressNote` promise.

Rebuild-backlog item 7 merges both legacy apps' note-taking surfaces —
Executive Planning Desktop's task-dropdown + note form, and Manager
Planner Desktop's Task+Notes window — since both call the same
`PlanningService.AddNoteAsync`/`GetNotesForTaskAsync` pair. Reading the
real legacy source directly
(`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:234-261`)
confirms the exact mechanics:

```csharp
public async Task<ProgressNote> AddNoteAsync(int taskId, string text, int authorId,
    int? meetingId = null, bool isPromise = false, DateTime? promisedDate = null,
    DateTime? noteDate = null)
{
    PlanningRules.ValidateNoteText(text);
    var effectiveDate = noteDate ?? DateTime.UtcNow;
    PlanningRules.ValidateNoteDate(effectiveDate);
    var note = new ProgressNote
    {
        WorkItemId = taskId,
        Text = text.Trim(),
        AuthorId = authorId,
        MeetingId = meetingId,
        IsPromise = isPromise,
        PromisedDate = promisedDate,
        NoteDate = effectiveDate
    };
    _db.ProgressNotes.Add(note);
    await _db.SaveChangesAsync();
    return note;
}

public Task<List<ProgressNote>> GetNotesForTaskAsync(int taskId) =>
    _db.ProgressNotes.Include(n => n.Author)
                     .Include(n => n.Meeting)
                     .Where(n => n.WorkItemId == taskId)
                     .OrderByDescending(n => n.NoteDate)
                     .ToListAsync();
```

Two things this confirms/corrects beyond domain-model.md's prose:

- **`GetNotesForTaskAsync` orders newest-`NoteDate`-first**
  (`OrderByDescending`), not the ascending "date-ordered timeline" a plain
  reading of functional-spec.md's "full, date-ordered note timeline" bullet
  might suggest.
- **`IsPromise` gates whether `PromisedDate` is actually persisted**, at
  the call site, not the service: both legacy view models pass
  `promisedDate: NewNoteIsPromise ? NewNotePromisedDate?.UtcDateTime : null`
  — an unchecked "promise" box always saves `PromisedDate = null` even if a
  date was picked before the box was unchecked.

`ManagerPlanner.Core.Validation.PlanningRules` in **this** repo already
has `ValidateNoteText`/`ValidateNoteDate` ported (added ahead of this item,
presumably during `scaffold-blazor-solution`) — but comparing them against
the real legacy `PlanningValidation.cs` shows the *rejection message text*
was paraphrased, not copied verbatim:

| Case | Legacy text | This repo's current text |
|---|---|---|
| Overlong note | "The note is too long. Keep it under 2000 characters." | "The note is too long — it cannot exceed 2000 characters." |
| Backdated too far | "That date is more than a month back. Notes can only be dated on or after {date}." | "The note date cannot be more than 1 month(s) in the past." |
| Future-dated | "A note cannot be dated in the future." | "The note date cannot be in the future." |

This item is the first to actually surface these three messages to a user
(nothing calls `ValidateNoteText`/`ValidateNoteDate` today), so it's the
right point to decide whether to correct them to the legacy text exactly —
see Open Questions.

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains two methods**, ported exactly from the legacy
   source above:
   - `AddNoteAsync(taskId, text, authorId, meetingId = null, isPromise = false, promisedDate = null, noteDate = null)`
     — validates via the existing `PlanningRules.ValidateNoteText`/
     `ValidateNoteDate`, defaults `noteDate` to `DateTime.UtcNow` exactly as
     the legacy code does, and persists a `ProgressNote` with `Text`
     trimmed.
   - `GetNotesForTaskAsync(taskId)` — same `Include(Author)`/
     `Include(Meeting)`/`OrderByDescending(NoteDate)` shape as the legacy
     query, so the rebuild's note history renders in the same order the
     legacy apps show it.

2. **`TaskRow.razor` gains a "Notes" section**, following the same nested,
   per-row pattern already established for the checklist tree (this
   rebuild has no dedicated "select a task" window the way either legacy
   app does — `task-management`'s proposal deferred a task-detail
   view, and no such view has been built since). Each row shows its note
   history (newest first, matching `GetNotesForTaskAsync`'s order) and an
   add-note form: free-text box, "This is a promise" checkbox, promised-date
   picker (enabled only when the checkbox is checked — mirroring the
   `IsPromise ? PromisedDate : null` gating above), a note-date picker
   (defaulting to today), and a "link to meeting" dropdown populated from
   the project's `Meeting` list already loaded by `ProjectDetail.razor`
   (`GetMeetingsForProjectAsync`, shipped in `meeting-recording-and-history`).

3. **Correct the three validation message strings** in
   `ManagerPlanner.Core.Validation.PlanningRules` to match the legacy text
   verbatim (see the table above) — see Open Questions for the exact
   proposed wording.

## Scope

### In Scope
- `PlanningService.AddNoteAsync(taskId, text, authorId, meetingId = null, isPromise = false, promisedDate = null, noteDate = null)`
- `PlanningService.GetNotesForTaskAsync(taskId)`
- A per-task "Notes" section on `TaskRow.razor`: note history (newest
  first) + add-note form (text, is-promise checkbox, promised-date picker,
  note-date picker, optional meeting-link dropdown)
- The `IsPromise`-gates-`PromisedDate` persistence rule, exactly as both
  legacy call sites implement it
- Wording fixes to `ValidateNoteText`'s and `ValidateNoteDate`'s three
  rejection messages, to match the legacy text verbatim

### Out of Scope
- **The Accountability report** (promised-vs-delivered `Verdict`,
  most-at-risk sorting) — separate, not-yet-built backlog item 8. This
  item only records notes; nothing here computes or displays a verdict.
- **A dedicated task-detail page/route.** No legacy-equivalent "select a
  task first" window is being introduced — notes are scoped inline to each
  `TaskRow`, consistent with how this rebuild already handles the
  checklist tree and how `meeting-recording-and-history` added meetings as
  a project-level list rather than a separate window.
- **Editing or deleting an existing note.** Neither legacy app exposes
  this (`ProgressNote` has no update/delete path in `PlanningService` at
  all) — out of scope for both apps equally, not a rebuild omission.
- **Task selection driving the Meetings/Notes tab**, as Executive Planning
  Desktop does (a task dropdown that filters which notes are shown) — this
  rebuild already shows every task inline per objective, so "select a
  task" is simply "this task's row," with no separate selection step
  needed.

## Impact

- **Files affected:** ~3 (estimated) — `PlanningService.cs` (2 new
  methods, no new file), `TaskRow.razor` (extended with a Notes section),
  `PlanningRules.cs` (3 message-string edits)
- **Complexity:** small — two directly-ported, already-precisely-specified
  service methods plus a UI addition following an established nested-panel
  pattern
- **Risk:** low — both service methods are mechanically exact against the
  legacy source (fixtures GM-005/GM-006/GM-007 already exist per
  rebuild-backlog.md); the only design questions are UI placement (resolved
  above by precedent) and the message-text fix below

## Open Questions

1. **Fix the three validation message strings to match the legacy text
   verbatim, or keep this repo's existing paraphrase?** Recommended: fix
   them now, since this is the first feature to actually surface them to a
   user, and diverging error text is exactly the kind of drift golden-master
   fidelity work (ADR-0005) exists to catch. Proposed exact replacements:
   - Overlong: `"The note is too long. Keep it under {MaxNoteText} characters."`
   - Backdated: `"That date is more than a month back. Notes can only be dated on or after {earliest:MMM dd, yyyy}."`
   - Future: `"A note cannot be dated in the future."`

   If you'd rather leave the current wording alone (e.g. because no
   stakeholder has asked for exact legacy string parity outside computed
   values), say so and this item will wire up the existing messages
   as-is.
2. **Meeting-link dropdown: required list source.** Recommended: reuse
   `ProjectDetail.razor`'s already-loaded `_meetings` list (no new service
   call) — a note can only ever link to a meeting already recorded against
   the same project, matching `ProgressNote.MeetingId`'s FK to `Meeting`
   (not cross-project).

---

**To proceed:** Review this proposal and approve to begin planning.
