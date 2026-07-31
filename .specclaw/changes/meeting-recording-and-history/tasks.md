# Tasks: Meeting recording and history

**Change:** meeting-recording-and-history
**Created:** 2026-07-31
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) add `GetMeetingsForProjectAsync` and
`AddMeetingAsync` to `PlanningService`, (2) build the "Meetings" section
on `ProjectDetail.razor` (record form + history list) and verify
end-to-end. No task links notes to meetings, wires
`DiscoveredInMeetingId`, or adds edit/delete for a meeting — those stay
out of scope per spec.md NFR2/AC8.

## Tasks

### Wave 1 — Core business logic

- [x] `T1` — Add `GetMeetingsForProjectAsync` and `AddMeetingAsync` to `PlanningService`
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at
    `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs:208-227`,
    not the doc summary. `GetMeetingsForProjectAsync(int projectId)`:
    via the factory-created context, `db.Meetings.Include(m =>
    m.Participant).Where(m => m.ProjectId == projectId).
    OrderByDescending(m => m.MeetingDate).ToListAsync()`. `AddMeetingAsync
    (int projectId, string title, MeetingType type, DateTime meetingDate,
    int? participantId)`: construct `new Meeting { ProjectId =
    projectId, Title = title, Type = type, MeetingDate = meetingDate,
    ParticipantId = participantId }`, add, `SaveChangesAsync()` — **no
    `PlanningRules.Validate*` call of any kind**, matching the legacy
    body exactly (do not add title validation here; spec.md FR2/FR6 —
    the caller validates, not the service). Add both as methods twelve
    and thirteen, after `ToggleChecklistItemAsync`, using
    `IDbContextFactory<PlanningDbContext>` like all eleven existing
    methods.

### Wave 2 — Page + verification

- [ ] `T2` — "Meetings" section on `ProjectDetail.razor`: record form, history list, end-to-end verification
  - Files: `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: Add a `MeetingType`/`Meeting` field set to the `@code` block:
    `private List<Meeting>? _meetings;`, `private string
    _newMeetingTitle = string.Empty;`, `private MeetingType
    _newMeetingType = MeetingType.VideoCall;`, `private int?
    _newMeetingParticipantId;`, `private DateTime? _newMeetingDate;`,
    `private string? _meetingErrorMessage;`. Load `_meetings` in both
    `OnInitializedAsync` and `RefreshAsync` alongside the existing four
    loads: `_meetings = await
    PlanningService.GetMeetingsForProjectAsync(Id);`.

    Add a "Meetings" `MudText Typo="Typo.h5"` heading after the existing
    Planner Grid content (spec.md FR3), matching the page's existing
    heading style. Below it, an `EditForm Model="this"
    OnValidSubmit="AddMeetingAsync"` containing: a `MudTextField
    @bind-Value="_newMeetingTitle"` (Label="Title"); a `MudSelect
    T="MeetingType" @bind-Value="_newMeetingType"` (Label="Type") with
    one `MudSelectItem Value="@type"` per `Enum.GetValues<MeetingType>()`
    — do **not** add a converter or humanize the text, `@type`'s default
    interpolation already renders the literal member name (spec.md
    FR4/design.md Key Decision 2); a `MudSelect T="int?"
    @bind-Value="_newMeetingParticipantId"` (Label="Participant") with a
    `MudSelectItem Value="@((int?)null)"` "— No participant —" option
    plus one item per `_teamMembers` (same field the assignee dropdown
    already uses — spec.md FR5, no new query); a `MudDatePicker
    @bind-Date="_newMeetingDate"` (Label="Date"); and a submit
    `MudButton`. If `_meetingErrorMessage` is non-null, render a
    `MudAlert Severity="Severity.Error"` above the form, matching the
    existing `_objectiveErrorMessage`/`_taskErrorMessage` pattern exactly.

    `AddMeetingAsync()` handler: `if
    (string.IsNullOrWhiteSpace(_newMeetingTitle)) { _meetingErrorMessage
    = "Enter a meeting title."; return; }` (spec.md FR6 — the exact
    legacy caller-side check, reproduced at the page level since the
    service itself validates nothing); otherwise call
    `PlanningService.AddMeetingAsync(Id, _newMeetingTitle.Trim(),
    _newMeetingType, _newMeetingDate ?? DateTime.UtcNow,
    _newMeetingParticipantId)` — **`DateTime.UtcNow`, not the legacy's
    local `DateTimeOffset.Now`** (spec.md NFR4/design.md Key Decision 4);
    on success reset `_newMeetingTitle = string.Empty;
    _newMeetingType = MeetingType.VideoCall; _newMeetingParticipantId =
    null; _newMeetingDate = null; _meetingErrorMessage = null;` and
    reload `_meetings = await
    PlanningService.GetMeetingsForProjectAsync(Id);` — matching
    `AddObjectiveAsync`/`AddTaskAsync`'s existing reset-and-reload shape
    on this same page.

    Below the form, render the history: if `_meetings is null`, "Loading
    meetings…"; else if `_meetings.Count == 0`, "No meetings yet."
    (spec.md Edge Cases, matching the existing "No objectives yet."/"No
    tasks yet." convention); else a `MudSimpleTable` with one row per
    meeting showing `Title`, `Type` (literal enum name), `MeetingDate`,
    and `Participant?.FullName ?? "—"` (spec.md AC5).

    Verify: `dotnet build` (AC1). Through the running app: submit the
    form with a title, a chosen type, a participant, and a date —
    confirm via direct DB inspection that exactly one new `Meeting` row
    persists with the trimmed title and the selected fields (AC2); leave
    the title blank and submit — confirm no new row is created and the
    inline error appears, with no unhandled exception (AC3); confirm the
    history list reflects every recorded meeting ordered by
    `MeetingDate` descending (AC4); confirm a meeting recorded with no
    participant shows the placeholder, not a blank cell (AC5); confirm
    the `MeetingType` dropdown's three options read exactly `VideoCall`/
    `PhysicalMeeting`/`PhoneCall` (AC6); confirm no other
    `PlanningService` method exists beyond the eleven existing ones plus
    these two (AC7); confirm no note-to-meeting link, no
    `DiscoveredInMeetingId` control, and no edit/delete affordance exists
    anywhere in the rendered output (AC8). Use `form_input`/JS dispatch
    per `.specclaw/context.md`'s documented fallback for claude-in-chrome
    interactions, and `read_page` immediately before every click.

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
