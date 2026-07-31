# Design: Meeting recording and history

**Change:** meeting-recording-and-history
**Created:** 2026-07-31

## Technical Approach

1. Add two methods to the existing `ManagerPlanner.Core/Services/
   PlanningService.cs` (no new file): `GetMeetingsForProjectAsync` and
   `AddMeetingAsync`, both ported verbatim from the real legacy source,
   following the established `IDbContextFactory<PlanningDbContext>`
   pattern (open/dispose a short-lived context per call).
2. Extend the existing `ProjectDetail.razor` with a new "Meetings"
   section, placed after the existing Planner Grid content: a record
   form (`EditForm`/`OnValidSubmit`, matching the page's existing
   Objective/Task form shape exactly) plus a read-only `MudSimpleTable`
   history list. Reuses the page's already-loaded `_teamMembers` field
   for the participant dropdown — no new query.
3. The form's submit handler pre-checks
   `string.IsNullOrWhiteSpace(_newMeetingTitle)` and trims before
   calling `AddMeetingAsync`, mirroring the real legacy caller
   (`MainWindowViewModel.AddMeetingAsync`) rather than relying on a
   `ValidationException` the service never throws.

No entity, schema, or migration changes — `Meeting`, `MeetingType`, and
every relevant cascade/`SetNull` rule already exist from
`scaffold-blazor-solution`'s `InitialCreate` migration.

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── ... eleven existing methods ...
├── GetMeetingsForProjectAsync(projectId)  [new]
└── AddMeetingAsync(projectId, title, type, meetingDate, participantId)  [new]

src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor   (extended)
├── existing: summary grid | Planner Grid (objectives/tasks/checklist)
├── new: "Meetings" heading
├── new: record-meeting EditForm
│   ├── MudTextField → _newMeetingTitle
│   ├── MudSelect<MeetingType> → _newMeetingType (renders literal enum names)
│   ├── MudSelect<int?> → _newMeetingParticipantId (reuses _teamMembers)
│   └── MudDatePicker → _newMeetingDate
├── new: AddMeetingAsync() handler
│   └── pre-check IsNullOrWhiteSpace → error message, else Trim() → PlanningService.AddMeetingAsync → reset + reload
└── new: read-only MudSimpleTable meeting history
    └── foreach _meetings (Title | Type | MeetingDate | Participant?.FullName ?? "—")
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `GetMeetingsForProjectAsync(projectId)`, + `AddMeetingAsync(projectId, title, type, meetingDate, participantId)` |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | + "Meetings" section: record form + read-only history list |

## Data Model Changes

None. `Meeting`, `MeetingType`, and the `Meeting`↔`Project`/`User`/
`ProgressNote`/`WorkItem` relationships already exist from
`scaffold-blazor-solution`'s migration.

## API Changes

None. No HTTP/JSON API — the page calls `PlanningService` directly, as
established in every prior change.

## Key Decisions

1. **`AddMeetingAsync` has zero validation, matching the legacy service
   body exactly.** The empty-title rejection lives at the page level
   (FR6), not the service — confirmed by tracing the real legacy caller
   (`MainWindowViewModel.cs:159-174`), not assumed from the service
   signature alone. Reusing this project's own error-message-field/
   `MudAlert` convention (already established for
   `AddObjectiveAsync`/`AddTaskAsync` on this same page) to surface the
   rejection, since there's no exception to catch here.
2. **`MeetingType` renders as literal enum member names** — "VideoCall",
   not "Video Call" — confirmed by reading the legacy ViewModel's
   binding source directly (`MainWindowViewModel.cs:24`, no
   `IValueConverter`/`ToString()` override anywhere). A plain
   `@type` interpolation in the `MudSelectItem` reproduces this for
   free; no custom converter needed in the rebuild either.
3. **Participant dropdown reuses the existing `_teamMembers` field**,
   not a new query — the same list `AddTaskAsync`'s assignee dropdown
   already loads via `GetTeamMembersAsync()`. One fewer service call per
   page load, and one fewer thing to keep in sync.
4. **The date-picker's "no date chosen" fallback uses `DateTime.UtcNow`,
   not the legacy caller's literal `DateTimeOffset.Now`.** The legacy
   value is local time; this project has already decided (constraint,
   `.specclaw/context.md`) that "no client-local-time concept exists
   anywhere in this app yet... don't introduce per-feature local-time
   formatting ad hoc." Using UTC here is the deliberate, documented
   deviation the constraint calls for, not a missed nuance.
5. **The "Meetings" section extends the existing `ProjectDetail.razor`
   page**, not a new route. ADR-0004 groups "the Projects, Planner
   Grid, Task+Notes, and Accountability windows" as web-native page
   sections rather than one page per legacy "window," and every prior
   backlog item (Objectives, Tasks, status, checklist) has already
   extended this same page rather than introducing a new one.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — `Meeting` entity fields,
  `MeetingType` enumeration, the `Meeting`→`ProgressNote`/`WorkItem`
  `SetNull` relationships.
- `.specclaw/adr/0004-mdi-shell-to-web-navigation.md` — "the Projects,
  Planner Grid, Task+Notes, and Accountability windows (items 1, 2,
  3/6/7, 8) become routed pages or a panel/tab layout," grounding Key
  Decision 5's "extend the existing page" choice.
- **Real legacy source**, read directly:
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:208-227` — the exact
    `GetMeetingsForProjectAsync`/`AddMeetingAsync` bodies (grounds
    FR1/FR2).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Desktop\
    ViewModels\MainWindowViewModel.cs:159-174` (`AddMeetingAsync`) — the
    caller-side title check/trim (grounds FR6/Key Decision 1).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Desktop\
    ViewModels\MainWindowViewModel.cs:24` — the unconverted
    `ObservableCollection<MeetingType>` binding (grounds FR4/Key
    Decision 2).
- `.specclaw/context.md` — the `IDbContextFactory` pattern (NFR1); the
  established `AddObjectiveAsync`/`AddTaskAsync` error-message-field/
  `MudAlert` shape this item reuses (FR6/Key Decision 1); the "no
  client-local-time" constraint (NFR4/Key Decision 4); the
  `GetProjectsAsync`/`GetTasksForProjectAsync` bare-`OrderByDescending`
  tie-break precedent this item's own identical gap follows (Edge
  Cases).

## Risks & Mitigations

- **Risk:** the page-level title check could be forgotten or bypassed in
  a future edit, since the service itself enforces nothing.
  **Mitigation:** explicitly documented here and in spec.md FR6/FR2 as
  a deliberate legacy-matching decision, not an oversight — a future
  editor reading either doc sees why the check lives where it does.
- **Risk:** reusing `_teamMembers` for the participant dropdown ties
  this feature's correctness to that field already being loaded before
  the Meetings section renders. **Mitigation:** `_teamMembers` is
  already populated in `OnInitializedAsync`/`RefreshAsync` before any
  page content renders (confirmed by reading the current file) — no
  new load-ordering risk introduced.
- **Risk:** the `MeetingDate` tie-break gap (no secondary sort key) could
  read as a bug during verification. **Mitigation:** documented
  explicitly in spec.md's Edge Cases as an intentional legacy-matching
  gap, with the exact precedent (`GetProjectsAsync`/
  `GetTasksForProjectAsync`) this project already treats the same way.
