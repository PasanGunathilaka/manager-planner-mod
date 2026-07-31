# Proposal: Meeting recording and history

**Created:** 2026-07-31
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

Rebuild-backlog item `BL-006` is next in dependency order after
`BL-001` (`Meeting.ProjectId` is a required FK to `Project`; `Meeting`
depends on nothing else). The `Meeting` domain entity, its `MeetingType`
enum, and its full `PlanningDbContext` configuration (cascade to
`ProgressNote`/`SetNull`, `SetNull` on `WorkItem.DiscoveredInMeeting`)
were all scaffolded up front in `scaffold-blazor-solution` — but no
`PlanningService` method and no UI for it exists anywhere in the rebuild
yet. functional-spec.md's Named Gap #1 is directly relevant: "`Manager
Planner Desktop` has no Meeting-recording capability at all... but the
newer, actively-published app... offers no UI to record a meeting,
browse meeting history, or link a note to one."

Rebuild-backlog.md flags this item's gate as `BLOCKED — blocked by
CQ-008 (Meeting recording: bring into the MDI shell, or leave as a
tabbed-app-only feature)`. Reading `clarifications.md`/`decisions.md`
directly, though, shows this is already resolved in substance:
**CQ-001** ("Canonical front-end: ExecutivePlanning.Desktop vs.
ManagerPlanner.Desktop") was answered on 2026-07-30 with "Option 3 —
Merge both legacy feature sets into one modern rebuilt UI. ManagerPlanner
.Desktop is treated as the newer operational reference, but useful
capabilities that exist only in ExecutivePlanning.Desktop, **such as
meeting recording** and full status management, must not be silently
dropped." CQ-008's own "Answer:" field is technically still blank (a gap
in cross-referencing, not a live open question) — but CQ-001's answer
directly names meeting recording as a capability that must be carried
into the rebuild. This proposal treats CQ-008 as resolved by CQ-001,
citing it explicitly rather than re-litigating the same decision twice.

Reading the real legacy source directly confirms the exact mechanics:

`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:208-227`:
```csharp
public Task<List<Meeting>> GetMeetingsForProjectAsync(int projectId) =>
    _db.Meetings.Include(m => m.Participant)
                .Where(m => m.ProjectId == projectId)
                .OrderByDescending(m => m.MeetingDate)
                .ToListAsync();

public async Task<Meeting> AddMeetingAsync(int projectId, string title, MeetingType type,
    DateTime meetingDate, int? participantId)
{
    var m = new Meeting
    {
        ProjectId = projectId,
        Title = title,
        Type = type,
        MeetingDate = meetingDate,
        ParticipantId = participantId
    };
    _db.Meetings.Add(m);
    await _db.SaveChangesAsync();
    return m;
}
```

Two things worth flagging, both confirmed by direct reads, not guesses:

1. **`AddMeetingAsync` performs zero validation** — unlike every other
   `Add*Async` method (`AddProjectAsync`/`AddTaskAsync`/`AddObjectiveAsync`
   all call a `PlanningRules.Validate*` method first; this one doesn't).
   But tracing the **caller**, per this project's own established
   "trace the full caller-to-service chain" pattern
   (`../manager-planner/src/ExecutivePlanning.Desktop/ViewModels/
   MainWindowViewModel.cs:159-174`):
   ```csharp
   private async Task AddMeetingAsync()
   {
       if (SelectedProject is null) { StatusMessage = "Select a project first."; return; }
       if (string.IsNullOrWhiteSpace(NewMeetingTitle)) { StatusMessage = "Enter a meeting title."; return; }

       await _service.AddMeetingAsync(
           SelectedProject.Id,
           NewMeetingTitle.Trim(),
           NewMeetingType,
           (NewMeetingDate ?? DateTimeOffset.Now).UtcDateTime,
           NewMeetingParticipant?.Id);
       ...
   }
   ```
   The empty/whitespace-title check and the trim both happen at the
   **caller**, not the service — the real end-to-end legacy behavior
   does reject an empty title, just via a page-level status message
   rather than a thrown `ValidationException`. This is the same shape
   `task-management` already found for `AddTaskAsync`'s `Description`
   trimming.
2. **The `MeetingType` dropdown shows the literal enum member name, not
   a humanized label.** `MainWindowViewModel.cs:24`:
   `MeetingTypes = new ObservableCollection<MeetingType>(Enum.GetValues<MeetingType>());`
   bound directly with no `IValueConverter` or `ToString()` override
   anywhere in the ViewModel or view — Avalonia's default `ComboBox`
   binding calls the enum's own `.ToString()`, so the legacy dropdown
   literally shows "VideoCall", "PhysicalMeeting", "PhoneCall", not
   "Video Call" with a space. This directly resolves rebuild-backlog.md's
   own previously-open verification item ("none of the four documents
   quote the rendered label text") — confirmed by reading the real
   binding source, not guessed.

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains two methods**, ported exactly from the
   legacy source above:
   - `GetMeetingsForProjectAsync(projectId)` — `.Include(m =>
     m.Participant)`, ordered `OrderByDescending(m => m.MeetingDate)`,
     using the established `IDbContextFactory<PlanningDbContext>`
     pattern.
   - `AddMeetingAsync(projectId, title, type, meetingDate,
     participantId)` — no validation call, matching the legacy service
     body exactly (see Problem, point 1).

2. **`ProjectDetail.razor` gains a Meetings section**, extending the
   same page every prior backlog item has extended (Planner Grid,
   status buttons, checklist) rather than introducing a new route —
   consistent with ADR-0004's grouping of "the Projects, Planner Grid,
   Task+Notes, and Accountability windows" as web-native page sections,
   and this project's own established "don't build UI ahead of need"
   discipline (the section arrives with its owning backlog item, not
   before). Contents:
   - A record-meeting form: title text field, a `MeetingType` dropdown
     showing the literal enum names (`VideoCall`/`PhysicalMeeting`/
     `PhoneCall`, matching the legacy exactly per Problem point 2), a
     participant dropdown sourced from the existing
     `GetTeamMembersAsync()` (the same source `AddTaskAsync`'s assignee
     dropdown already uses), and a date picker.
   - The page's own submit handler pre-checks
     `string.IsNullOrWhiteSpace(title)` and trims before calling
     `AddMeetingAsync` — reproducing the real legacy caller behavior
     from Problem point 1, not just the service signature.
   - A read-only, date-descending list of the project's meetings,
     showing title, type, date, and participant name (or "—" if unset).

## Scope

### In Scope
- `PlanningService.AddMeetingAsync(projectId, title, type, meetingDate,
  participantId)` and `GetMeetingsForProjectAsync(projectId)` — ported
  exactly, including the service-level no-validation behavior
- A "Meetings" section on `ProjectDetail.razor`: record form + read-only
  history list, per the capability bullets above
- Page-level empty/whitespace-title rejection and trimming, matching the
  real legacy end-to-end caller behavior
- The `MeetingType` dropdown showing literal enum member names

### Out of Scope
- **Linking a progress note to a meeting** (`ProgressNote.MeetingId`) —
  that's backlog item BL-007 (Progress notes and promise tracking),
  not yet built; this item only creates and lists `Meeting` rows.
- **Wiring `WorkItem.DiscoveredInMeetingId`** — a separate, already-
  logged non-blocking question (CQ-012); `task-management` already
  decided not to wire this, and nothing about recording a meeting
  requires revisiting that.
- **Editing or deleting a meeting** — no legacy UI exposes either
  affordance for a single `Meeting` row.
- **Any tabbed/windowed reproduction of "Meetings & Notes"** — ADR-0004
  already settled that MDI/tab chrome is re-interpreted as web
  navigation, not reproduced; this item is a plain page section like
  every other one so far.

## Impact

- **Files affected:** ~2 (estimated) — `PlanningService.cs` (2 new
  methods, no new file), `ProjectDetail.razor` (extended with a new
  section)
- **Complexity:** small — the domain entity, enum, and schema
  configuration already exist in full; this item is service methods +
  a form + a list, the same shape every prior vertical-feature item has
  taken
- **Risk:** low — both service methods are exact, already-quoted ports
  with no forking behavior; the one real nuance (caller-side validation
  and the literal-enum-name dropdown) is resolved here by direct source
  evidence, not left as a guess

## Open Questions

None. Both potential forks this item could have raised — whether meeting
recording is in scope at all (CQ-008), and what text the `MeetingType`
dropdown should show — are resolved above by direct evidence (CQ-001's
existing answer, and the legacy binding source respectively), not left
for a human decision.

---

**To proceed:** Review this proposal and approve to begin planning.
