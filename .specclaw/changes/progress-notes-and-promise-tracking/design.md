# Design: Progress notes and promise tracking

**Change:** progress-notes-and-promise-tracking
**Created:** 2026-08-03

## Technical Approach

1. Add two methods to the existing `ManagerPlanner.Core/Services/
   PlanningService.cs` (no new file): `AddNoteAsync` and
   `GetNotesForTaskAsync`, both ported verbatim from the real legacy
   source, following the established `IDbContextFactory<PlanningDbContext>`
   pattern (open/dispose a short-lived context per call).
2. Correct the three note-validation rejection message strings in
   `ManagerPlanner.Core/Validation/PlanningRules.cs` (`ValidateNoteText`'s
   overlong message, `ValidateNoteDate`'s backdated and future-dated
   messages) to match the legacy text verbatim — the validators' logic
   (trim-before-length-check, `nowUtc`-injectable date comparison) is
   already correct and untouched; only the three message literals change.
3. Extend the existing `TaskRow.razor` with a new "Notes" section,
   following the same per-row, always-visible pattern already established
   for the checklist tree: a note history list (newest first) plus an
   add-note form (`EditForm`/`OnValidSubmit`, matching this page's
   existing Objective/Task/Meeting form shape). `TaskRow` owns and
   refreshes its own note list locally (loaded in `OnInitializedAsync`),
   the same row-owned-state pattern `ChecklistTree.razor` already uses for
   checklist toggles — no new `EventCallback` to `ProjectDetail`.
4. `TaskRow` gains a new `[Parameter] public List<Meeting> Meetings`,
   passed from `ProjectDetail.razor`'s already-loaded `_meetings` field —
   no new query, reusing the same "pass an already-loaded list down"
   pattern the participant dropdown established for `_teamMembers`.

No entity, schema, or migration changes — `ProgressNote` and every
relevant relationship/cascade rule already exist from
`scaffold-blazor-solution`'s `InitialCreate` migration.

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── ... thirteen existing methods ...
├── AddNoteAsync(taskId, text, authorId, meetingId?, isPromise, promisedDate?, noteDate?)  [new]
└── GetNotesForTaskAsync(taskId)  [new]

src/ManagerPlanner.Core/Validation/PlanningRules.cs   (message-text fix only)
├── ValidateNoteText — overlong message corrected
└── ValidateNoteDate — backdated + future-dated messages corrected

src/ManagerPlanner.Web/Components/Pages/TaskRow.razor   (extended)
├── existing: title/deadline/badges cell | owner/status cell | checklist cell
├── new: [Parameter] List<Meeting> Meetings
├── new: "Notes" cell
│   ├── read-only note history (newest first: text | note date | promise-due-date-or-none | author | meeting title-or-placeholder)
│   ├── add-note EditForm
│   │   ├── MudTextField (multiline) → _newNoteText
│   │   ├── MudCheckBox → _newNoteIsPromise
│   │   ├── MudDatePicker (Disabled when !_newNoteIsPromise) → _newNotePromisedDate (default: UtcNow.AddDays(7))
│   │   ├── MudDatePicker → _newNoteDate (default: UtcNow.Date)
│   │   └── MudSelect<int?> → _newNoteMeetingId (from Meetings parameter; "— No meeting —" → null)
│   └── AddNoteAsync() handler
│       └── PlanningService.AddNoteAsync(WorkItem.Id, text, authorId, meetingId, isPromise, isPromise ? promisedDate : null, noteDate)
│           → catch ValidationException → error message field
│           → on success: reset fields to defaults, reload _notes via GetNotesForTaskAsync
└── OnInitializedAsync(): _notes = await PlanningService.GetNotesForTaskAsync(WorkItem.Id)

src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor   (extended)
└── both <TaskRow .../> usages (per-objective loop, Ungrouped section) gain Meetings="_meetings"
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `AddNoteAsync(taskId, text, authorId, meetingId?, isPromise, promisedDate?, noteDate?)`, + `GetNotesForTaskAsync(taskId)` |
| `src/ManagerPlanner.Core/Validation/PlanningRules.cs` | Modify | Correct `ValidateNoteText`'s overlong message and `ValidateNoteDate`'s two rejection messages to the legacy text verbatim |
| `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` | Modify | + `[Parameter] List<Meeting> Meetings`, + "Notes" cell: history list + add-note form, + `OnInitializedAsync` note load |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | Pass `Meetings="_meetings"` to both existing `<TaskRow>` usages |

## Data Model Changes

None. `ProgressNote` and its `WorkItem`/`Meeting`/`User` relationships
already exist from `scaffold-blazor-solution`'s migration.

## API Changes

None. No HTTP/JSON API — the component calls `PlanningService` directly,
as established in every prior change.

## Key Decisions

1. **`TaskRow` owns its own note list, loaded once in
   `OnInitializedAsync` and reloaded locally after a successful add** —
   not threaded through `ProjectDetail`'s summary/objective state, and no
   `EventCallback` raised to the parent. Nothing on `ProjectDetail`
   (summary counts, objective grouping) derives from note state, exactly
   the same reasoning `nested-checklist-items-and-grid-status-badges`
   already applied to checklist toggles (`.specclaw/context.md` Recent
   Decision 4).
2. **The meeting-link dropdown reuses `ProjectDetail.razor`'s existing
   `_meetings` field**, passed down as a new `TaskRow` parameter — not a
   new per-row `GetMeetingsForProjectAsync` call. One fewer service call
   per row, and the same "pass an already-loaded list down" shape the
   participant dropdown already established for `_teamMembers`
   (`meeting-recording-and-history` Key Decision 3).
3. **`IsPromise` gates `PromisedDate` at the component handler, not the
   service** — confirmed by reading both real legacy view model callers
   directly (`ExecutivePlanning.Desktop/ViewModels/MainWindowViewModel.cs:187-188`
   and `ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:163-164`), both
   of which pass `isPromise ? promisedDate : null` rather than relying on
   the service to ignore an irrelevant date. `AddNoteAsync` itself accepts
   whatever `promisedDate` it's given verbatim, matching the legacy
   service body exactly.
4. **Promised-date picker defaults to +7 days from today (in UTC)** —
   reproduces the real legacy `ExecutivePlanning.Desktop` view model's
   `_newNotePromisedDate = DateTimeOffset.Now.AddDays(7)` initial value
   (`MainWindowViewModel.cs:64`), substituting `DateTime.UtcNow` for
   `DateTimeOffset.Now` per this project's no-local-time constraint — the
   same "keep the legacy default, convert to UTC" treatment already
   applied to the meeting date-picker's fallback
   (`meeting-recording-and-history` Key Decision 4).
5. **The three validation message strings are corrected to the legacy
   text verbatim**, not left as this repo's existing paraphrase — this is
   the first feature to actually surface `ValidateNoteText`/
   `ValidateNoteDate`'s rejection messages to a user, and the exact text
   is now pinned by captured golden-master fixtures (`GM-005`, `GM-006`,
   `GM-007`), not just a doc summary. Per ADR-0005, exact message wording
   is named as a fidelity target for this specific item.
6. **The "Notes" section extends the existing `TaskRow.razor` component**,
   not a new page/route. ADR-0004 groups the legacy "windows" into web
   pages/panels with no per-task detail window reproduced; every prior
   backlog item (checklist, status, meetings) has already extended an
   existing component/page rather than introducing a new one, and a task
   row is already the natural per-task scope in this rebuild's grid-based
   layout.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — `ProgressNote` entity fields,
  DR-005 (note text ≤2000 chars) and DR-006 (one-month backdate window),
  the `WorkItem`/`Meeting`/`User` relationships.
- `.specclaw/baseline/scenarios.md` (`GM-005`, `GM-006`, `GM-007`) and
  their fixtures (`.specclaw/baseline/fixtures/GM-00{5,6,7}.json`) — the
  exact captured legacy rejection message text and boundary values,
  grounding FR6/AC5-AC8.
- `.specclaw/adr/0004-mdi-shell-to-web-navigation.md` — "the Projects,
  Planner Grid, Task+Notes, and Accountability windows... become routed
  pages or a panel/tab layout," grounding Key Decision 6's "extend the
  existing component" choice.
- `.specclaw/adr/0005-fidelity-verification-strategy.md` — "exact
  validation/error-message wording (items 6, 7)" named explicitly as a
  fidelity target, grounding Key Decision 5.
- **Real legacy source**, read directly:
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:234-261` — the exact `AddNoteAsync`/
    `GetNotesForTaskAsync` bodies (grounds FR1/FR2).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningValidation.cs:53-76` — the exact rejection message
    text for all three validators (grounds FR6).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Desktop\
    ViewModels\MainWindowViewModel.cs:64,187-188` — the promised-date
    default and the `isPromise ? promisedDate : null` gating (grounds
    FR4/FR5/Key Decision 3/4).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    ViewModels\MainViewModel.cs:161-165` — confirms the same
    `isPromise`-gating in the second legacy app, plus its explicit
    `noteDate` picker (grounds FR3's note-date picker).
- `.specclaw/context.md` — the `IDbContextFactory` pattern (NFR1); the
  "checklist toggles update local component state only" precedent (Key
  Decision 1); the "pass an already-loaded list down" precedent for
  `_teamMembers` (Key Decision 2); the "no client-local-time concept"
  constraint (NFR3/Key Decision 4); the `GetProjectsAsync`/
  `GetMeetingsForProjectAsync` bare-`OrderByDescending` tie-break
  precedent this item's own identical gap follows (Edge Cases).

## Risks & Mitigations

- **Risk:** correcting the three validation message strings could be seen
  as scope creep on a file (`PlanningRules.cs`) this item wasn't proposed
  to touch. **Mitigation:** the proposal named this exact fix as Open
  Question 1, with a recommendation to make the correction now (since this
  is the first feature to surface these messages) — this design accepts
  that recommendation explicitly rather than silently expanding scope.
- **Risk:** the `IsPromise`-gates-`PromisedDate` logic living in the
  component handler (not the service) could be bypassed or "simplified
  away" in a future edit. **Mitigation:** documented explicitly here and
  in spec.md FR4/AC3 as a deliberate legacy-matching decision, with both
  real legacy call sites cited, not an incidental implementation detail.
- **Risk:** `TaskRow` now needs a `Meetings` parameter from its parent;
  a future page that renders `TaskRow` without passing it would NPE or
  render an empty dropdown. **Mitigation:** default the parameter to an
  empty list (`= new()`), so a missing `Meetings` binding degrades to "no
  meetings selectable" rather than throwing — the same defensive default
  pattern this codebase already uses for collection-typed parameters.
- **Risk:** the `NoteDate` tie-break gap (no secondary sort key) could
  read as a bug during verification. **Mitigation:** documented explicitly
  in spec.md's Edge Cases as an intentional legacy-matching gap, with the
  exact precedent (`GetProjectsAsync`/`GetMeetingsForProjectAsync`) this
  project already treats the same way.
