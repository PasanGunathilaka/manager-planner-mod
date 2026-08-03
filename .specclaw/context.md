# Project Context

_Last updated: 2026-08-03 — after "progress-notes-and-promise-tracking"._

## Architecture Overview

A two-project .NET 8 solution (`ManagerPlanner.sln`) rebuilding the legacy
Avalonia desktop app (`ExecutivePlanning.Core` + two desktop shells) as a
single Blazor web app:

- **`src/ManagerPlanner.Core`** (class library) — the domain/persistence
  layer. Holds entities (`Domain/`), validation (`Validation/PlanningRules.cs`),
  the EF Core `DbContext` + migrations (`Data/`, `Migrations/`), and business
  logic (`Services/PlanningService.cs`, `Services/Reports.cs`). This
  project's only dependency is `Microsoft.EntityFrameworkCore.Sqlite`
  (+ `.Design`, dev-only), matching the legacy `ExecutivePlanning.Core.csproj`'s
  shape.
- **`src/ManagerPlanner.Web`** (Blazor Server, unified .NET 8 "Blazor Web
  App" template, with **global** Interactive Server render mode — see
  below) — references `.Core` directly. No API/DTO boundary between them
  (components call `PlanningService` directly), per ADR-0002's
  flat-service-surface guidance. **MudBlazor 9.7.0** is the app's
  component/CSS framework (`ui-modernization`) — the only UI dependency,
  registered via `builder.Services.AddMudServices()` in `Program.cs` and
  `@using MudBlazor` in `Components/_Imports.razor`.

Data flow: `Program.cs` registers `PlanningDbContext` via
`AddDbContextFactory<T>`, `PlanningService` (Scoped), and MudBlazor's
services (`AddMudServices()`), applies pending EF Core migrations at
startup (`Database.Migrate()`), bootstraps a single Manager `User` if none
exists, then serves Razor components. Feature pages so far: `/projects`
(browse + create) and `/projects/{id}` (summary + refresh, plus a Planner
Grid: add-objective form, per-objective task rows, and a single unified
"Add task" form covering the full `WorkItem` field set — title, objective,
assignee, deadline, description, "discovered in a meeting" checkbox — plus
a Meetings section: a record-meeting form and a read-only history list).
Tasks with no `ObjectiveId` render in a separate "Ungrouped" section, shown
only when non-empty. Each task row is rendered by a shared `TaskRow.razor`
component with four cells: its first `<td>` shows title + deadline plus
two conditional badges — an "OVERDUE" caption (`MudText`, `Color.Error`)
when `Deadline` is in the past and `Status != Done`, and a "⚑ discovered"
caption (`MudText`, `Color.Warning`) when `IsDiscovered` — two computed
properties (`IsOverdue`/`IsDiscovered`) added alongside the file's existing
`StatusText`/`StatusColor` computed properties, matching the legacy
`RowViewModels.cs` predicates term for term
(`nested-checklist-items-and-grid-status-badges`); its second `<td>` shows
assignee-or-"Unassigned" + a color-coded `MudChip` status badge, plus a
`MudButtonGroup` of four inline status-change buttons — "Not started" /
"In progress" / "Blocked" / "Mark done" — that call
`PlanningService.ChangeStatusAsync` directly and notify the parent page via
a parameterless `StatusChanged` `EventCallback` (wired to `ProjectDetail`'s
full `RefreshAsync`, since the page's summary counts depend on status);
its third `<td>` renders a new recursive `ChecklistTree.razor` component
when the task has any root-level checklist items (`WorkItem.Checklist.Any(c
=> c.ParentId == null)`), else keeps the original `&mdash;` placeholder —
one `MudCheckBox<bool>` per item (label + optional `"— {FullName}"`
assignee text), recursing into itself for each item's children ordered by
`SortOrder`, with no hard-coded depth limit. Unlike the status buttons,
ticking a checklist checkbox calls `PlanningService.ToggleChecklistItemAsync`
directly and updates only local component state (`item.IsDone`) — it has no
`EventCallback` parameter at all and never triggers `ProjectDetail`'s
`RefreshAsync`, because no summary/aggregate on the page derives from
checklist-completion state. This closes out rebuild-backlog item BL-005 —
the last unbuilt piece of the Planner Grid's per-task cell — as an
extension of the existing Task creation/viewing surface, not a new
vertical slice. `meeting-recording-and-history` (BL-006) then added a
"Meetings" section to this same `ProjectDetail.razor` page — a
record-meeting form (title, `MeetingType`, participant dropdown reusing the
page's existing `_teamMembers`, date picker) plus a read-only,
`MeetingDate`-descending history table — rather than a new route,
extending the page for a fifth time running instead of introducing a
dedicated Meetings page (see Key Patterns). `progress-notes-and-promise-tracking`
(BL-007) then added `TaskRow.razor`'s fourth cell — a per-task Notes
section — rather than another `ProjectDetail.razor`-level section, since
notes are task-scoped rather than project-scoped: a read-only note history
(`NoteDate`-descending; each row shows note text, note date, a
promised-date line only when `IsPromise` is true, author, and the linked
meeting's title or an em-dash placeholder) plus an add-note form (text, an
"is a promise" checkbox gating a promised-date picker, a note-date picker
defaulting to today, and a meeting-link dropdown). The dropdown is fed by a
new `[Parameter] public List<Meeting> Meetings`, passed down from
`ProjectDetail.razor`'s already-loaded `_meetings` field (no new query) —
the same pass-an-already-loaded-list-down shape `_teamMembers` established
for the assignee dropdown. `TaskRow` owns and loads this note list itself
(`OnInitializedAsync`) and never calls `ProjectDetail`'s `RefreshAsync` —
the same row-owned-local-state shape `ChecklistTree` established for
checklist toggles, now proven on a second per-row capability. These are the
first four slices of the legacy app's feature surface (Project management,
Objective grouping, Task creation/viewing, Task status transitions), now
uniformly restyled on MudBlazor (`ui-modernization`, the fifth merged
change and the first pure cross-cutting UI change rather than a new
vertical feature slice — zero `PlanningService`/`PlanningRules`/entity/
migration changes anywhere in it). "Switching the active project" is URL
navigation between `/projects/{id}` rows; there is no separate "current
project" session state.

`MainLayout.razor` is now a real `MudLayout` app shell — `MudAppBar` +
`MudDrawer`/`MudNavMenu` with `MudNavLink`s to `/` and `/projects` — plus
the four root Mud provider components (`MudThemeProvider`,
`MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) living
exactly once, app-wide, in this one file. Future backlog items
(Accountability reporting, BL-008 — still not built) can add another
`MudNavLink` here if they warrant a dedicated route; every capability
shipped so far — including Meetings and now Notes/promise-tracking — has
instead landed as a new section on an existing detail page rather than a
new page/nav entry (see Key Patterns).

## Coding Style & Conventions

- **.NET 8**, `<Nullable>enable</Nullable>` on both projects.
- Entities in `ManagerPlanner.Core.Domain` are plain mutable classes (not
  records): `int Id` PK, `get; set;` auto-properties, `ICollection<T>`
  (not `List<T>`) for navigation collections initialized to `new List<T>()`.
  Required reference-type navigations default to `null!` (EF sets them on
  load); required strings default to `string.Empty`.
- **Entity property defaults are load-bearing, not decorative** — e.g.
  `Project.CreatedUtc = DateTime.UtcNow`, `User.IsActive = true`,
  `WorkItem.CreatedUtc = DateTime.UtcNow`. The legacy `PlanningService`
  relies on these being set at the entity level (it never sets them at the
  call site), so when porting entities, always check whether the legacy
  service constructor-initializes a field or leans on the entity default —
  don't assume "the field exists" is enough.
- Validation lives in `ManagerPlanner.Core.Validation.PlanningRules` (static
  class + `ValidationException`). Validators **trim before length-checking**
  and date validation uses `DateTime.UtcNow` (never local `DateTime.Today`)
  with an injectable `nowUtc` parameter for testability — this matches the
  legacy `PlanningValidation.cs` exactly; don't simplify it back to
  untrimmed/local-time checks. `progress-notes-and-promise-tracking`
  corrected three of `PlanningRules`' rejection-message string literals
  (`ValidateNoteText`'s overlong-note message, `ValidateNoteDate`'s
  backdated and future-dated messages) to match the legacy text verbatim —
  the validation *logic* was already correct; only the literal strings had
  drifted from legacy wording. Cross-check a validator's exact message text
  against the legacy source (or a captured golden-master fixture), not just
  its pass/fail logic.
- Business logic lives in `ManagerPlanner.Core.Services.PlanningService` —
  one method per legacy operation (fifteen so far), each opening/disposing
  its own `PlanningDbContext` via the injected `IDbContextFactory` (see Key
  Patterns). Read-model DTOs (e.g. `ProjectSummary`) live alongside it in
  `Services/Reports.cs`, matching the legacy file split. Not every method
  is a straight port: `GetUngroupedTasksForProjectAsync` has no legacy
  equivalent (added because the unified add-task form can produce a task
  with `ObjectiveId == null`), and `AddTaskAsync` deliberately drops the
  legacy `discoveredInMeetingId` parameter — no UI links a `WorkItem` to a
  `Meeting` yet. `meeting-recording-and-history` added the ability to
  *create* Meetings (`AddMeetingAsync`) but deliberately no UI control sets
  `WorkItem.DiscoveredInMeetingId` or otherwise links a task/note to a
  meeting (confirmed in that change's verify pass) — that linkage remains a
  separate, not-yet-built capability, not an oversight. `ChangeStatusAsync`
  and `ToggleChecklistItemAsync` (methods ten/eleven,
  `nested-checklist-items-and-grid-status-badges`), `GetMeetingsForProjectAsync`/
  `AddMeetingAsync` (methods twelve/thirteen, `meeting-recording-and-history`),
  and now `AddNoteAsync`/`GetNotesForTaskAsync` (methods fourteen/fifteen,
  `progress-notes-and-promise-tracking`) are all verbatim ports with no
  signature deviation beyond the established `IDbContextFactory` pattern —
  `ToggleChecklistItemAsync`'s body (`item.IsDone = isDone;
  item.CompletedUtc = isDone ? DateTime.UtcNow : null;
  SaveChangesAsync()`) is identical to the real legacy
  `ExecutivePlanning.Core/Services/PlanningService.cs`, and so is
  `AddMeetingAsync`'s (`new Meeting { ProjectId, Title, Type, MeetingDate,
  ParticipantId }; db.Meetings.Add(m); await db.SaveChangesAsync();`) —
  except `AddMeetingAsync` carries **zero validation at the service
  layer**: the empty-title check and `.Trim()` happen only in
  `ProjectDetail.razor`'s caller, mirroring the real legacy
  `MainWindowViewModel.AddMeetingAsync` caller rather than the service (see
  the caller-to-service call-chain note below). **`AddNoteAsync` breaks
  that no-service-validation pattern** — unlike `AddMeetingAsync`, it *does*
  call `PlanningRules.ValidateNoteText`/`ValidateNoteDate` before opening
  the DB context, exactly matching the real legacy service body. Don't
  assume every `Add*Async` method shares `AddMeetingAsync`'s
  caller-only-validation shape — check each method's own legacy body
  individually. `GetNotesForTaskAsync` orders by `NoteDate` **descending**
  (`OrderByDescending`), matching the legacy body exactly, not the
  ascending "date-ordered timeline" a plain reading of domain-model.md's
  prose might suggest. `ui-modernization` touched zero lines of this file
  or `Validation/` — confirmed by an empty `git diff` across the whole
  change; it is a pure Razor/markup restyle.
- **Render mode is now set globally, once, in `App.razor` — not per page.**
  `<HeadOutlet @rendermode="InteractiveServer" />` and
  `<Routes @rendermode="InteractiveServer" />` cover the whole app
  (`ui-modernization`, needed because MudBlazor's `MudDialogProvider`/
  `MudPopoverProvider`/`MudSnackbarProvider` require an interactive render
  context to exist at all). **This supersedes the earlier per-page rule**
  ("Feature pages needing interactivity MUST declare `@rendermode
  InteractiveServer` explicitly") — the redundant per-page directives that
  used to live on `Projects.razor`/`ProjectDetail.razor` were removed as
  part of this change. Any *new* page added going forward does **not**
  need its own `@rendermode` directive; it inherits interactivity from the
  global setting.
- **`MudSelect<T>` natively supports `@bind-Value` for nullable value types
  (e.g. `MudSelect<int?>`)** — use it directly for nullable-int dropdowns
  (Objective/Assignee/Meeting pickers, "— Ungrouped —"/"— Unassigned —"/
  "— No meeting —" → `null`) instead of a manual `@onchange` handler that
  parses the selected string. `task-management` originally used manual
  `OnObjectiveSelected`/`OnAssigneeSelected` handlers as a workaround
  because plain HTML `<select>` binding to `int?` wasn't trusted at the
  time; `ui-modernization` replaced both with `MudSelect<int?>` and deleted
  the manual handlers as dead code — same field semantics, same `null`
  mapping, fewer lines. `progress-notes-and-promise-tracking`'s new
  meeting-link dropdown for notes reuses `MudSelect<int?>` the same way.
  Don't reintroduce manual `@onchange` parsing for a new nullable dropdown;
  reach for `MudSelect<T>` first.
- **Never name a Blazor component parameter (or other identifier) `Task` in
  `ManagerPlanner.Web`** — `ImplicitUsings` is enabled, so `Task` collides
  with `System.Threading.Tasks.Task`; any async method referencing the bare
  `Task` type in the same component then breaks. `TaskRow.razor`'s
  parameter is `[Parameter] public WorkItem WorkItem`, not `Task` — a
  design-vs-implementation deviation caught before shipping
  (task-management, 2026-07-28).
- **Trace the full caller-to-service call chain when porting a method for
  fidelity, not just the method signature.** The legacy `AddTaskAsync`
  service body stores `Description` verbatim (no `.Trim()`), which looks
  like "don't trim" if you only read the service — but the real legacy
  *caller* (`MainWindowViewModel.AddTaskAsync`) pre-processes it
  (`string.IsNullOrWhiteSpace(...) ? null : ...Trim()`) before calling the
  service. `ProjectDetail.razor`'s page handler now applies that same
  pre-processing before calling `PlanningService.AddTaskAsync`, matching
  the legacy app's true end-to-end behavior rather than one link of it
  (task-management, 2026-07-28). The same lesson repeated verbatim in
  `meeting-recording-and-history`: `AddMeetingAsync`'s service body has no
  validation at all, and it's `ProjectDetail.razor`'s handler that guards
  `IsNullOrWhiteSpace(_newMeetingTitle)` and calls `.Trim()` before
  invoking the service — confirmed against the real legacy
  `MainWindowViewModel.AddMeetingAsync` caller, not just
  `PlanningService.AddMeetingAsync`'s own body (meeting-recording-and-history,
  2026-07-31). `progress-notes-and-promise-tracking` shows the inverse case
  is just as important to verify: `TaskRow.razor`'s `AddNoteAsync()`
  handler wraps its call in `catch (ManagerPlanner.Core.Validation.ValidationException
  ex) { _noteErrorMessage = ex.Message; }`, surfacing whatever
  `PlanningRules` rejects — and the ternary it builds
  (`_newNoteIsPromise ? _newNotePromisedDate : null`) was confirmed against
  *both* real legacy desktop apps' ViewModel callers
  (`ExecutivePlanning.Desktop/ViewModels/MainWindowViewModel.cs:188` and
  `ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:164`), not just one —
  this feature merges both legacy apps' note-taking surfaces onto the one
  `AddNoteAsync`/`GetNotesForTaskAsync` pair, so both callers needed
  checking.

## Key Patterns

- **`IDbContextFactory<PlanningDbContext>` via `AddDbContextFactory`, never
  `AddDbContext`** — required for Blazor Server to avoid one `DbContext`
  instance being shared/reused unsafely across a circuit's concurrent
  renders (ADR-0002). Every future component that touches the database
  should inject the factory and create/dispose a short-lived context per
  operation, not hold a long-lived injected `DbContext`. `ChangeStatusAsync`
  (the tenth `PlanningService` method), `ToggleChecklistItemAsync` (the
  eleventh, `nested-checklist-items-and-grid-status-badges`),
  `GetMeetingsForProjectAsync`/`AddMeetingAsync` (the twelfth/thirteenth,
  `meeting-recording-and-history`), and now `AddNoteAsync`/
  `GetNotesForTaskAsync` (the fourteenth/fifteenth,
  `progress-notes-and-promise-tracking`) all follow this exactly — same as
  every method before them.
- **EF Core migrations live inside `ManagerPlanner.Core`**, not `.Web` —
  via `PlanningDbContextFactory : IDesignTimeDbContextFactory<PlanningDbContext>`
  in `Core/Data/`. This lets `dotnet ef migrations add`/`database update`
  run standalone against `Core` without needing `.Web` built or referenced
  as the tooling's startup project.
- **Ground-truth against the real legacy source before trusting doc
  summaries, design docs, or agent output.** The actual legacy repo is
  checked out at `C:\Learnings\Projects\manager-planner` (sibling
  directory) — `src/ExecutivePlanning.Core/{Domain,Data,Services}` plus the
  two desktop shells' `ViewModels`/`Views`. The `.specclaw/analysis/*.md`
  docs are prose summaries, not a substitute for it. This has now paid off
  seven times running: a mistyped `User.OwnedTasks` type and missed entity
  defaults in item 0; the exact `ProjectSummary.PercentComplete` rounding
  formula in item 1; the real end-to-end `Description`-trimming behavior
  living in the legacy ViewModel *caller* in `task-management`; in
  `task-status-transitions`, the exact `ChangeStatusAsync` body, Executive
  Planning Desktop's four-button order/labels, and confirmation (read at
  both legacy call sites) that neither ever supplies a `Reason`; in
  `nested-checklist-items-and-grid-status-badges`, confirming that the real
  legacy `GetPlannerForProjectAsync` has the *same* Include-chain gap this
  rebuild's version has (no `.ThenInclude(c => c.Assignee)` under
  `Checklist`, so a checklist item's assignee only ever resolves via EF's
  automatic relationship-fixup) — replicating that gap was the fidelity-
  correct move, not a bug to fix; in `meeting-recording-and-history`,
  confirming both `GetMeetingsForProjectAsync`/`AddMeetingAsync`'s exact
  bodies **and** that the real legacy `MainWindowViewModel.AddMeetingAsync`
  caller — not the service — does the empty-title check and trim; and, in
  `progress-notes-and-promise-tracking`, confirming both `AddNoteAsync`/
  `GetNotesForTaskAsync`'s exact bodies — including that `AddNoteAsync`
  (unlike `AddMeetingAsync`) *does* carry service-layer validation — plus
  the exact three corrected `PlanningRules` message strings and their
  boundary conditions (empty/2000/2001-char note text; one-month-back and
  future note dates), cross-checked against the real legacy
  `PlanningValidation.cs` and both legacy desktop apps' ViewModel callers,
  plus captured golden-master fixtures `GM-005.json`/`GM-006.json`/
  `GM-007.json` as an independent second check alongside direct source
  reading. Read the legacy source directly at every layer (entity,
  service, caller, and UI), not just the layer being ported. (`ui-modernization`
  remains the one change with no legacy-fidelity dimension at all — a pure
  rendering restyle with no legacy UI framework to port from, since the
  legacy app is Avalonia desktop XAML, not a web component library.)
- **Multi-collection `Include` chains need `.AsSplitQuery()` once the
  child collections are non-trivial.** Both `GetPlannerForProjectAsync`
  and `GetUngroupedTasksForProjectAsync` carry `.AsSplitQuery()` (the
  latter added during `task-management`'s verify pass, after the build step
  initially added it only to the former) — keep both in sync if either's
  `Include`/`ThenInclude` shape changes again. Confirmed still unchanged by
  `nested-checklist-items-and-grid-status-badges` (no new `ThenInclude` was
  added for `Checklist.Assignee` — see the Ground-truth pattern above).
  `GetMeetingsForProjectAsync` (`meeting-recording-and-history`) has only a
  single `.Include(m => m.Participant)`, and `GetNotesForTaskAsync`
  (`progress-notes-and-promise-tracking`) has two flat `.Include`s
  (`Author`, `Meeting`), so no split-query need has arisen at either site.
- **A shared row/list-item component is worth extracting the moment two
  render sites in the *same* change need identical markup** — not
  speculatively ahead of need. `TaskRow.razor` was extracted during
  `task-management` because that change itself introduced two call sites
  (per-objective loop, Ungrouped section), mirroring the legacy app's own
  named `TaskRowVm` row concept. `task-status-transitions` then extended
  it with the emerging **child-calls-service-then-notifies-parent**
  shape: the child component (`TaskRow`) calls `PlanningService` directly,
  then invokes a parameterless `StatusChanged` `EventCallback`; the parent
  (`ProjectDetail.razor`) wires that callback to its **existing full**
  `RefreshAsync` method rather than writing a new lighter one, so
  aggregate state derived elsewhere on the page (the summary's Done/
  InProgress/Blocked/NotStarted/Overdue counts) stays correct immediately,
  not just the row itself. Reuse the full refresh by default for this
  shape unless it's proven too expensive. **That shape isn't universal,
  though**: `nested-checklist-items-and-grid-status-badges`'s
  `ChecklistTree.razor` deliberately has no `EventCallback` parameter at
  all — it calls `PlanningService.ToggleChecklistItemAsync` then mutates
  only its own local `item.IsDone`, because nothing else on the page
  derives from checklist-completion state. `progress-notes-and-promise-tracking`
  confirms this decide-per-feature rule generalizes: `TaskRow`'s new Notes
  section also skips the bubble-and-refresh shape entirely, loading and
  owning its note list locally (`OnInitializedAsync`) with no callback to
  `ProjectDetail`, because nothing on the page's summary derives from
  note/promise state either. Two of `TaskRow`'s three sub-features
  (checklist, notes) are now local-state-only; only the status buttons
  bubble up, because only status feeds the page-level summary counts.
  Decide per feature whether a bubble-and-refresh callback is actually
  needed before wiring one up; don't add it reflexively just because
  `TaskRow`'s status buttons have one.
- **Pass an already-loaded page-level list down as a component
  `[Parameter]` rather than re-querying inside a child component.**
  `ProjectDetail.razor` loads `_teamMembers`/`_meetings` once per page load;
  `TaskRow`'s assignee dropdown already consumed `_teamMembers` this way,
  and `progress-notes-and-promise-tracking`'s new meeting-link dropdown for
  notes reuses the identical shape via a new `[Parameter] public
  List<Meeting> Meetings` rather than adding a per-row query. Reach for
  this whenever a child component needs a small, already-loaded,
  page-scoped reference list.
- **Recursive Blazor components for self-similar tree data — one
  component, not one per depth level.** `ChecklistTree.razor` (new,
  `nested-checklist-items-and-grid-status-badges`) takes a
  `List<ChecklistItem> Items` parameter, renders one `MudCheckBox<bool>`
  per item (label + optional `"— {FullName}"` assignee text), and recurses
  into `<ChecklistTree Items="item.Children.OrderBy(c =>
  c.SortOrder).ToList()" />` for any item with children — no hard-coded
  depth limit, terminates naturally at leaves, mirroring the legacy
  `RowViewModels.cs`'s own `BuildTree` (`byParent[null].OrderBy(...)` for
  roots, `byParent[m.Id].OrderBy(...)` recursively for children). It lives
  directly in `Components/Pages/` alongside `TaskRow.razor` — no new
  `Shared/` folder was introduced for one component, matching the existing
  flat layout. Follow this same recursive-component-in-`Pages/` shape for
  any future self-similar tree UI rather than special-casing depth levels.
- **Every backlog item so far extends `ProjectDetail.razor`/`TaskRow.razor`
  with a new section rather than introducing a new route.**
  `meeting-recording-and-history`'s Meetings capability — a record-meeting
  form plus a read-only, `MeetingDate`-descending history table — was
  added as a new section on the existing page, the same shape every prior
  vertical slice (Objective grouping, Task creation, Task status
  transitions, nested checklists) used;
  `progress-notes-and-promise-tracking` then did the same one level down,
  adding its Notes history/form as a new cell on the existing `TaskRow`
  rather than a page-level section, since notes are task-scoped. This
  updates the assumption baked into `MainLayout.razor`'s original nav-menu
  note ("future pages just add another `MudNavLink`") — a new capability
  doesn't necessarily need a new route/nav entry; check whether it belongs
  on an existing detail page (or existing row component) first before
  scaffolding a new one.
- **Enums render via their own `.ToString()` — no humanizer, no
  display-name converter, anywhere in the UI.** `MeetingType`'s dropdown
  (`@foreach (var type in Enum.GetValues<MeetingType>())` →
  `<MudSelectItem Value="@type">@type</MudSelectItem>`) and the meeting
  history table both render the literal member names (`VideoCall`,
  `PhysicalMeeting`, `PhoneCall`), confirmed by reading the real legacy
  binding source directly — it does the same. Don't add a
  switch-expression/Humanizer-style formatter for a future enum dropdown
  unless the legacy source proves the real app does one; default to plain
  `.ToString()` (meeting-recording-and-history).
- **`GetCurrentManagerIdAsync()` + a startup Manager-user bootstrap stand in
  for authentication**, which doesn't exist yet. `Program.cs` guarantees
  exactly one `User` with `Role = Manager` on first startup; any feature
  needing an "owner"/"current user" calls `PlanningService.GetCurrentManagerIdAsync()`
  rather than assuming a signed-in user, resolving it **fresh on every
  call** rather than caching it on the component — `TaskRow.razor`'s
  per-click status handler and its new `AddNoteAsync` handler
  (`progress-notes-and-promise-tracking`) both do this the same way
  `Projects.razor`'s `AddProjectAsync` handler already did. Expected to be
  replaced, not extended, once a real auth ADR exists (ADR-0001 defers
  this).
- **Parallel-agent delegation for disjoint-file sub-tasks within one
  wave.** When a change's tasks touch completely non-overlapping files
  (e.g. `ui-modernization`'s Wave 2: Home/Error vs. Projects vs.
  ProjectDetail/TaskRow), spawn the coding agents in parallel rather than
  sequentially — no git worktree isolation is needed since nothing
  collides, and it cuts real wall-clock time substantially (3 tasks
  finished in roughly one sequential task's worth of wait, all first-try
  with 0 build errors). Default to this whenever a wave's tasks are
  file-disjoint.
- **Verify a third-party library's exact API surface against the
  actually-installed package before writing code against it**, rather than
  relying on training-data recall of its API. `ui-modernization`'s largest
  task (`ProjectDetail.razor`/`TaskRow.razor`, converting to
  `MudSelect<int?>`/`MudDatePicker`/`MudSimpleTable`/`MudChip`/
  `MudButtonGroup`) had its coding agent write a small throwaway reflection
  console app against the installed `MudBlazor.dll` (9.7.0) to confirm
  exact property names (`MudDatePicker.Date`, `MudSimpleTable.Hover`/
  `Dense`, `MudChip<T>.Color`, etc.) first — result: zero MudBlazor
  API-mismatch compile errors on the first build attempt for the most
  complex file in the change. Use this whenever precision against an
  unfamiliar third-party API matters more than speed.
- **Commit `tasks.md`/`STATUS.md` immediately before `specclaw-build
  finalize`, every time — this is now a settled, working habit, not a
  live risk.** `finalize` requires a clean working tree to check out
  `master` for the branch-per-change merge; `specclaw-update-task-status`
  mutates `tasks.md`'s checkboxes without committing them, which blocked
  `finalize` once (`task-status-transitions`, L16). Running `git status`
  and committing any pending `tasks.md`/`STATUS.md` changes right before
  calling `finalize` has now made the merge succeed on the first attempt
  repeatedly (`task-status-transitions`, `ui-modernization`) — keep doing
  it as routine, not as a fix applied only when something looks wrong.
- **Testing Blazor Server pages via claude-in-chrome:** use the `form_input`
  tool for text fields, never `computer.type` (simulated keystrokes raced
  against the SignalR round-trip and corrupted values during
  `project-management`'s verification). Always call `read_page` immediately
  before a click with no intervening tool calls — refs go stale across
  Blazor's async re-renders, and a stale-ref click can make a genuinely
  working feature look broken. If a click seems to do nothing, diff
  server-log query counts before/after before concluding the feature is
  broken. **Default straight to JS-dispatched clicks (`element.click()`
  via `javascript_tool`), skipping the real-click attempt entirely** —
  real mouse-click dispatch via claude-in-chrome has now wedged in every
  one of the last five changes' verification sessions (`planner-grid`,
  `project-management`, `task-management`, `task-status-transitions`,
  `ui-modernization`), each time recovered immediately by JS dispatch, so
  this is the established default for this project, not a fallback to
  diagnose into. Pair it with a scratch console app (or direct DB
  inspection) querying the live SQLite file when persisted-state evidence
  from the browser alone is in doubt. **`nested-checklist-items-and-grid-status-badges`
  and `meeting-recording-and-history` both shipped with no such runtime/DB
  verification artifact** despite each verify pass flagging the gap
  (`verify-report.md`'s sole Issue, twice running) — both were instead
  verified by exact code-level parity against already-tested/ported legacy
  logic. **`progress-notes-and-promise-tracking` broke that pattern**: per
  its `status.md`, a coding agent drove the running app via
  `claude-in-chrome` and cross-checked persisted SQLite rows for every
  acceptance criterion, including exact validation-message text at the
  empty/2000/2001-char and one-month-back/future-date boundaries —
  independently confirmed by the verify pass's code-plus-golden-master-fixture
  re-check, with no discrepancy found. Keep defaulting to live browser/DB
  verification when a test project doesn't exist; this change shows it's
  still the stronger check when actually run.
- **`tasks.md` line-wrapping silently breaks `specclaw-parse-tasks`** — hit
  twice now (`task-management`'s T2, then `task-status-transitions`'s T2
  again). If a task's title line or `Files:` line wraps across two lines
  in the markdown, the parser silently drops that task's files/depends/
  estimate fields, with no warning pointing at the actual broken task
  (only an unrelated "malformed task" warning on the template-legend
  line). Always author each task's title and `Files:` line as one
  unwrapped line in `tasks.md`, no matter how long, and re-run
  `specclaw-parse-tasks` immediately after writing it — treat any
  unexpectedly empty files/depends/estimate field in its output as a red
  flag to go inspect the raw markdown, not a fluke.
- **No client-local-time concept exists anywhere in this app yet.**
  Deadlines render as UTC `yyyy-MM-dd` (`TaskRow.razor`), not the legacy
  desktop app's local-time `MMM dd` format, and overdue checks already
  compare purely in UTC (`GetProjectSummaryAsync`, and now `TaskRow`'s
  `IsOverdue` computed property). Don't introduce per-feature local-time
  formatting ad hoc — ADR-0001 left the client-timezone question
  explicitly open; resolve it once, project-wide, when it's actually
  decided. `meeting-recording-and-history`'s date-picker follows the same
  rule at a new site: its "no date chosen" fallback (`_newMeetingDate ??
  DateTime.UtcNow` in `ProjectDetail.razor`'s `AddMeetingAsync` handler)
  deliberately uses `DateTime.UtcNow`, not the real legacy caller's local
  `DateTimeOffset.Now` — a conscious deviation from literal
  caller-fidelity, chosen because this project's no-local-time constraint
  outranks matching that one legacy detail. `progress-notes-and-promise-tracking`'s
  note-date and promised-date pickers both default to UTC values
  (`_newNoteDate` to `UtcNow.Date`, `_newNotePromisedDate` to
  `UtcNow.AddDays(7)`), continuing the same rule.
- **Cross-check validator/service message text and boundary conditions
  against captured golden-master fixtures, not just the legacy source
  reading alone.** `progress-notes-and-promise-tracking` used
  `.specclaw/baseline/fixtures/GM-005.json`/`GM-006.json`/`GM-007.json` to
  independently confirm `PlanningRules`' exact rejection strings and the
  precise empty/2000/2001-char and one-month-back/tomorrow date boundaries
  — an additional, automatable check alongside direct legacy-source
  reading, useful whenever a golden-master capture already exists for the
  validator being ported.

## Technology Decisions

- **Blazor Server** (not WASM or a separate Web API), per ADR-0002 —
  maximizes reuse of `.Core`, no API/DTO boundary the legacy app never had.
- **SQLite**, per ADR-0003's recommendation for a single-tenant pilot;
  connection string lives in `appsettings.json` (`ConnectionStrings:PlanningDatabase`)
  so switching to SQL Server/PostgreSQL later is a config change.
- **EF Core Migrations from the first scaffold** — explicitly replaces the
  legacy `PlanningDbContextFactory.Create`'s `EnsureCreated()` (ADR-0003).
  One migration exists so far: `InitialCreate`. `task-status-transitions`
  needed no new migration — `StatusChange` and `WorkItem.CompletedUtc`
  already existed in that schema. `ui-modernization` needed no migration
  either — it touches only `.Web`, never `.Core`.
  `nested-checklist-items-and-grid-status-badges` likewise needed none —
  `ChecklistItem` and its cascade/`Restrict` rules already existed in
  `InitialCreate`, independently confirmed at 100% parity against the
  legacy golden master by the most recent `/specclaw:verify-parity` run.
  `meeting-recording-and-history` needed none either — `Meeting`,
  `MeetingType`, and every relevant cascade/`SetNull` rule (including
  `WorkItem.DiscoveredInMeeting`'s `SetNull`) were already scaffolded in
  `InitialCreate` by `scaffold-blazor-solution`, confirmed unchanged by
  this change's `git diff` touching only `PlanningService.cs` and
  `ProjectDetail.razor`. `progress-notes-and-promise-tracking` needed none
  either — `ProgressNote` and every relevant relationship (`Author`,
  `Meeting`, `WorkItem`) already existed in `InitialCreate`, confirmed by
  this change's diff touching only `PlanningService.cs`, `PlanningRules.cs`,
  `ProjectDetail.razor`, and `TaskRow.razor`.
- **.NET 8** — matches the legacy solution's target framework exactly, to
  avoid a version gap ahead of future fidelity comparisons.
- **MudBlazor 9.7.0** as the component/CSS framework (`ui-modernization`)
  — a single NuGet package, no npm/JS build step, first-class Blazor
  Server support on .NET 8, and no CSS/component framework existed at all
  before this change (not even Bootstrap). Chosen over building custom CSS
  because it ships complete form controls (`MudSelect`, `MudDatePicker`),
  layout primitives (`MudLayout`/`MudAppBar`/`MudDrawer`), and dialog/
  snackbar/popover infrastructure the not-yet-built Accountability page
  will need. Its bundled CSS/JS (`_content/MudBlazor/MudBlazor.min.css`/
  `.min.js`) is the only new asset — no external CDN reference (e.g.
  Google Fonts) was added, preserving the project's local-first character
  (matches the SQLite-only backend's no-external-network-dependency
  pattern). Version left unpinned in the design step by choice but
  resolved to `9.7.0` at restore time and is now pinned in
  `ManagerPlanner.Web.csproj`.
- **Badges and other meaning-carrying UI use MudBlazor's semantic `Color`
  enum, not custom CSS/hex values.** The original color-coded `MudChip`
  status badge established this; `nested-checklist-items-and-grid-status-badges`
  extends it to two new `TaskRow` badges — `Color.Error` for the "OVERDUE"
  caption, `Color.Warning` for the "⚑ discovered" caption — both plain
  `MudText` components, no custom styling. Reach for MudBlazor's built-in
  palette (`Color.Error`/`Warning`/`Success`/etc.) for any future
  meaning-carrying indicator rather than introducing ad hoc colors.

## Constraints

- **Do not add `PlanningService` or any feature UI casually** — the
  scaffold is deliberately infrastructure-only (ADR-0002's sequencing
  intent: "item 0 (scaffold)... before item 1 stays a pure feature
  change"). Business logic arrives with its owning backlog item, not ad
  hoc. `ui-modernization` reaffirmed the UI half of this too: it
  deliberately did not build any Notes/Meetings/Accountability UI ahead of
  their owning backlog items even though a shell nav item would have been
  easy to add — restyle only what's built.
- **Don't silently "fix" the accountability verdict precedence** or other
  quirks the analysis docs flag as intentional-looking legacy behavior
  (e.g. the `IsOverdue`-checked-before-promise-pending order in
  `AccountabilityRow.Verdict`) — ADR-0005 requires a golden-master capture
  and an explicit product decision before deviating, not a "senior-engineer
  cleanup." Now that `ProgressNote` creation/read exists
  (`progress-notes-and-promise-tracking`), this is directly relevant: the
  not-yet-built Accountability reporting item (BL-008) reads a task's
  *latest* `ProgressNote` promise to compute `Verdict` — don't build that
  view's precedence logic ahead of its own backlog item either.
- **Don't silently "restore" a legacy fast-path when a change deliberately
  replaces it.** `task-management` replaced Manager Planner Desktop's
  per-objective inline fast-add (title-only, `assigneeId: null`, hardcoded
  `+7 days` deadline) with a single unified "Add task" form covering all
  fields — a proposal-approved decision, not an oversight. Don't
  reintroduce the coarse path as a second add-task route without a fresh
  product decision.
- **Don't add a `Reason` input or a confirmation dialog to the status-change
  buttons.** `task-status-transitions` confirmed by reading both legacy
  call sites directly (`ExecutivePlanning.Desktop`'s `SetStatusAsync` and
  `ManagerPlanner.Desktop`'s `MarkDone`) that neither ever supplies a
  `Reason`, and neither shows a confirmation before a status change
  (unlike task/project deletion, items 9/10). `StatusChange.Reason` stays
  in the schema, unset, until a future item deliberately decides to expose
  it. (`MudDialog`/`IDialogService` is the intended mechanism once
  deletion — items 9/10 — actually ships a confirmation dialog;
  `ui-modernization` established the provider is wired but didn't use it
  for anything yet.)
- **Don't add per-status button disabling to `TaskRow`'s status controls.**
  All four buttons (Not started/In progress/Blocked/Mark done) stay
  visible and clickable regardless of the row's current status, matching
  legacy exactly — `ChangeStatusAsync`'s no-op guard is what makes a
  redundant same-status click harmless, not UI-level prevention. Adding
  `disabled` logic would be an unrequested deviation from the ported
  behavior.
- **Keep `MudSimpleTable`, not a full `MudTable` rewrite, for the Planner
  Grid** unless a concrete future need for `MudTable`'s templated/
  data-bound features (built-in sorting, filtering, paging) actually
  arises. `ui-modernization` deliberately wrapped the *existing*
  `<thead>`/`<tbody>`/`<TaskRow>` markup in `MudSimpleTable` (a
  styling-only wrapper) rather than restructuring `TaskRow`'s
  embedded-status-buttons-per-row pattern into `MudTable`'s API — a
  full `MudTable` rewrite would be a real, higher-risk restructuring, not
  a drop-in upgrade. The Meetings history table
  (`meeting-recording-and-history`) and the new Notes history list
  (`progress-notes-and-promise-tracking`) both follow the same rule — a
  plain read-only `MudSimpleTable`/list, not `MudTable`.
- **No UI creates or deletes a checklist item, or edits its assignee —
  `ToggleChecklistItemAsync` is the only checklist mutation that exists.**
  `nested-checklist-items-and-grid-status-badges` confirmed exactly 11
  `PlanningService` methods existed at that point (10 existing + this
  one); `GetMeetingsForProjectAsync`/`AddMeetingAsync` and now
  `AddNoteAsync`/`GetNotesForTaskAsync` were added since (fifteen total),
  but none of them touch checklist items — adding create/delete/
  assignee-edit for checklist items remains a separate, not-yet-decided
  backlog item, not something to bolt onto `ChecklistTree` incidentally.
- **Don't add `.ThenInclude(c => c.Assignee)` under the `Checklist`
  collection in `GetPlannerForProjectAsync`/`GetUngroupedTasksForProjectAsync`
  to "complete" the Include chain.** The real legacy
  `GetPlannerForProjectAsync` has the identical gap — a checklist item's
  `Assignee` resolves only via EF's automatic relationship-fixup, never an
  explicit `Include` — confirmed by reading the legacy source directly
  (`ExecutivePlanning.Core/Services/PlanningService.cs:136-142`).
  Replicating the gap is correct fidelity; "fixing" it would be an
  unrequested deviation.
- **Don't add a secondary sort key to `GetNotesForTaskAsync`'s
  `OrderByDescending(n => n.NoteDate)` for notes sharing an identical
  date.** The real legacy service has the same single-key sort gap
  (already true of `GetProjectsAsync`/`GetMeetingsForProjectAsync` too) —
  documented as an accepted edge case in
  `progress-notes-and-promise-tracking`'s verify report, not a bug to fix.
- **`git.strategy: branch-per-change`'s `finalize` step auto-merges the
  feature branch into `master` locally** — it does not leave the branch
  open for a separate GitHub PR. If a real reviewable PR is wanted for a
  future change, that needs deciding *before* running `/specclaw:build`
  (or accept that `/specclaw:pr` will find head==base and a straight
  `git push` is the only option left). Confirmed a sixth time on
  `nested-checklist-items-and-grid-status-badges`, a seventh time on
  `meeting-recording-and-history`, and an eighth time on
  `progress-notes-and-promise-tracking`.
- **Don't add a `ValidationException`/`PlanningRules` check to
  `AddMeetingAsync`.** The real legacy service has none; the empty-title
  check and `.Trim()` belong at the caller (`ProjectDetail.razor`),
  matching the real legacy `MainWindowViewModel.AddMeetingAsync`, the same
  caller-side-validation shape already established for `AddTaskAsync`'s
  `Description` handling. (Contrast `AddNoteAsync`, which *does* validate
  at the service layer — don't generalize one method's shape to another
  without checking its own legacy body.)
- **Don't add a humanizer/display-name converter for `MeetingType` (or any
  other enum) without checking the legacy source first.** Plain
  `.ToString()` literal rendering (`VideoCall`/`PhysicalMeeting`/
  `PhoneCall`) is confirmed correct fidelity, not a placeholder awaiting
  polish.
- **Don't replace the meeting date-picker's `DateTime.UtcNow` "no date
  chosen" fallback with the legacy caller's local `DateTimeOffset.Now`.**
  The project's no-local-time constraint (ADR-0001, and the existing
  UTC-deadline/overdue rule) takes priority over matching that one legacy
  detail literally.
- **Don't add a UI control that links a task/note to a Meeting, or that
  edits/deletes a `Meeting`.** `meeting-recording-and-history` added only
  creation (`AddMeetingAsync`) and read (`GetMeetingsForProjectAsync`); no
  control sets `WorkItem.DiscoveredInMeetingId`, and there's no edit/delete
  for `Meeting` — those are separate, not-yet-decided backlog items.
- **Don't add a UI control that edits or deletes an existing
  `ProgressNote`, and don't build an Accountability/`Verdict` view yet.**
  `progress-notes-and-promise-tracking` added only creation
  (`AddNoteAsync`) and read (`GetNotesForTaskAsync`) — confirmed by its
  full diff touching only `PlanningService.cs` (+35/-0, additive),
  `PlanningRules.cs` (message-string edits only), `ProjectDetail.razor`
  (+2/-2, just the new `Meetings` parameter on two `<TaskRow>` tags), and
  `TaskRow.razor` (+101, additive). Both note edit/delete and the
  Accountability/`Verdict` view (rebuild-backlog item 8, which depends on
  this one) remain separate, not-yet-built capabilities.

## Recent Decisions

1. **`AddNoteAsync` ported with full service-layer validation
   (`PlanningRules.ValidateNoteText`/`ValidateNoteDate`) — unlike
   `AddMeetingAsync`'s caller-only validation.** Confirmed by reading the
   real legacy service body directly; don't assume a uniform
   validation-shape across similarly-named `Add*Async` methods
   (progress-notes-and-promise-tracking, 2026-08-03).
2. **Three `PlanningRules` rejection-message strings (note-too-long,
   backdated, future-dated) corrected to verbatim legacy text**, cross-checked
   against both the real legacy source and captured golden-master fixtures
   `GM-005.json`/`GM-006.json`/`GM-007.json` — the validators' logic was
   already correct; only the literal message strings had drifted
   (progress-notes-and-promise-tracking, 2026-08-03).
3. **`TaskRow` now owns and loads its own Notes list locally
   (`OnInitializedAsync`), with no `EventCallback` to `ProjectDetail`.**
   Continues `ChecklistTree`'s row-owned-local-state shape onto a second
   per-row capability, since nothing on the page's summary derives from
   note/promise state (progress-notes-and-promise-tracking, 2026-08-03).
4. **`AddMeetingAsync` carries zero validation at the service layer — the
   page-level caller does the check.** `ProjectDetail.razor`'s
   `AddMeetingAsync()` handler does the `IsNullOrWhiteSpace` guard and
   `.Trim()` itself before calling `PlanningService.AddMeetingAsync`,
   exactly mirroring the real legacy ViewModel caller rather than the
   service (which never validates) — the same caller-side-validation shape
   as `AddTaskAsync`'s `Description` trimming
   (meeting-recording-and-history, 2026-07-31).
5. **Meetings was added as a new section on the existing
   `ProjectDetail.razor` page, not a new route.** Continues the
   established pattern of every prior backlog item extending this same
   page rather than introducing a new page/nav entry per feature
   (meeting-recording-and-history, 2026-07-31).
