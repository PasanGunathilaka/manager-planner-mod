# Project Context

_Last updated: 2026-07-28 — after "task-status-transitions"._

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
  App" template with Interactive Server render mode) — references `.Core`
  directly. No API/DTO boundary between them (components call `PlanningService`
  directly), per ADR-0002's flat-service-surface guidance.

Data flow: `Program.cs` registers `PlanningDbContext` via
`AddDbContextFactory<T>` and `PlanningService` (Scoped), applies pending EF
Core migrations at startup (`Database.Migrate()`), bootstraps a single
Manager `User` if none exists, then serves Razor components. Feature pages
so far: `/projects` (browse + create) and `/projects/{id}` (summary +
refresh, plus a Planner Grid: add-objective form, per-objective task rows,
and a single unified "Add task" form covering the full `WorkItem` field set
— title, objective, assignee, deadline, description, "discovered in a
meeting" checkbox). Tasks with no `ObjectiveId` render in a separate
"Ungrouped" section, shown only when non-empty. Each task row is rendered
by a shared `TaskRow.razor` component (title + deadline, assignee-or-
"Unassigned" + status text, plus four inline status-change buttons —
"Not started" / "In progress" / "Blocked" / "Mark done" — that call
`PlanningService.ChangeStatusAsync` directly and notify the parent page via
a parameterless `StatusChanged` `EventCallback`; no checklist or badges
yet — those remain separate not-yet-built backlog items). These are the
first four slices of the legacy app's feature surface (Project management,
Objective grouping, Task creation/viewing, Task status transitions).
"Switching the active project" is URL navigation between `/projects/{id}`
rows; there is no separate "current project" session state.

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
  untrimmed/local-time checks.
- Business logic lives in `ManagerPlanner.Core.Services.PlanningService` —
  one method per legacy operation (ten so far), each opening/disposing its
  own `PlanningDbContext` via the injected `IDbContextFactory` (see Key
  Patterns). Read-model DTOs (e.g. `ProjectSummary`) live alongside it in
  `Services/Reports.cs`, matching the legacy file split. Not every method
  is a straight port: `GetUngroupedTasksForProjectAsync` has no legacy
  equivalent (added because the unified add-task form can produce a task
  with `ObjectiveId == null`), and `AddTaskAsync` deliberately drops the
  legacy `discoveredInMeetingId` parameter (nothing can supply one yet —
  `Meeting` doesn't exist). `ChangeStatusAsync`, by contrast, is a verbatim
  port with no signature deviation beyond the established
  `IDbContextFactory` pattern.
- Feature pages needing interactivity (button clicks, form submits) MUST
  declare `@rendermode InteractiveServer` explicitly — the Blazor Web App
  template's default static server rendering does not process `@onclick`
  handlers at all; forgetting this makes a page look right but silently do
  nothing on click.
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
  (task-management, 2026-07-28).

## Key Patterns

- **`IDbContextFactory<PlanningDbContext>` via `AddDbContextFactory`, never
  `AddDbContext`** — required for Blazor Server to avoid one `DbContext`
  instance being shared/reused unsafely across a circuit's concurrent
  renders (ADR-0002). Every future component that touches the database
  should inject the factory and create/dispose a short-lived context per
  operation, not hold a long-lived injected `DbContext`. `ChangeStatusAsync`
  (the tenth `PlanningService` method, added in `task-status-transitions`)
  follows this exactly, same as every method before it.
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
  four times running: a mistyped `User.OwnedTasks` type and missed entity
  defaults in item 0; the exact `ProjectSummary.PercentComplete` rounding
  formula in item 1; the real end-to-end `Description`-trimming behavior
  living in the legacy ViewModel *caller* in `task-management`; and, in
  `task-status-transitions`, the exact `ChangeStatusAsync` body, Executive
  Planning Desktop's four-button order/labels, and confirmation (read at
  both legacy call sites) that neither ever supplies a `Reason` — read the
  legacy source directly at every layer (entity, service, caller, and UI),
  not just the layer being ported.
- **Multi-collection `Include` chains need `.AsSplitQuery()` once the
  child collections are non-trivial.** Both `GetPlannerForProjectAsync`
  and `GetUngroupedTasksForProjectAsync` carry `.AsSplitQuery()` (the
  latter added during `task-management`'s verify pass, after the build step
  initially added it only to the former) — keep both in sync if either's
  `Include`/`ThenInclude` shape changes again.
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
  shape unless it's proven too expensive.
- **`GetCurrentManagerIdAsync()` + a startup Manager-user bootstrap stand in
  for authentication**, which doesn't exist yet. `Program.cs` guarantees
  exactly one `User` with `Role = Manager` on first startup; any feature
  needing an "owner"/"current user" calls `PlanningService.GetCurrentManagerIdAsync()`
  rather than assuming a signed-in user, resolving it **fresh on every
  call** rather than caching it on the component — `TaskRow.razor`'s
  per-click status handler does this the same way `Projects.razor`'s
  `AddProjectAsync` handler already did. Expected to be replaced, not
  extended, once a real auth ADR exists (ADR-0001 defers this).
- **Testing Blazor Server pages via claude-in-chrome:** use the `form_input`
  tool for text fields, never `computer.type` (simulated keystrokes raced
  against the SignalR round-trip and corrupted values during
  `project-management`'s verification). Always call `read_page` immediately
  before a click with no intervening tool calls — refs go stale across
  Blazor's async re-renders, and a stale-ref click can make a genuinely
  working feature look broken. If a click seems to do nothing, diff
  server-log query counts before/after before concluding the feature is
  broken. If clicks fail *wholesale* (even plain `<a href>` links do
  nothing, on a fresh tab too) — a wedged Chrome renderer, not an app bug —
  dispatch via in-page JavaScript (`element.click()`) first; this has now
  been the working fallback **four** changes running (`planner-grid`,
  `project-management`, `task-management`, `task-status-transitions`), each
  time paired with a scratch console app querying the live SQLite file
  directly to confirm persisted state when browser evidence alone was in
  doubt. This is now frequent enough that it's worth defaulting straight to
  JS-dispatched clicks for any claude-in-chrome verification on this
  project, rather than re-diagnosing the same wedge every time.
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
- **`specclaw-build finalize` requires a clean working tree before it can
  check out `master` for the branch-per-change merge.** `specclaw-update-
  task-status` mutates `tasks.md`'s `[ ]`→`[x]` checkboxes directly, but
  neither that command nor `specclaw-build commit` (which only stages a
  task's own declared files) commits that change — so it's routinely left
  uncommitted on the feature branch. `finalize` then fails with "Your local
  changes... would be overwritten." Always run `git status` and commit any
  pending `tasks.md`/`STATUS.md` changes immediately before calling
  `specclaw-build finalize`, every time, not just when something looks
  wrong (first hit in `task-status-transitions`).
- **No client-local-time concept exists anywhere in this app yet.**
  Deadlines render as UTC `yyyy-MM-dd` (`TaskRow.razor`), not the legacy
  desktop app's local-time `MMM dd` format, and overdue checks already
  compare purely in UTC (`GetProjectSummaryAsync`). Don't introduce
  per-feature local-time formatting ad hoc — ADR-0001 left the
  client-timezone question explicitly open; resolve it once, project-wide,
  when it's actually decided.

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
  already existed in that schema.
- **.NET 8** — matches the legacy solution's target framework exactly, to
  avoid a version gap ahead of future fidelity comparisons.

## Constraints

- **Do not add `PlanningService` or any feature UI casually** — the
  scaffold is deliberately infrastructure-only (ADR-0002's sequencing
  intent: "item 0 (scaffold)... before item 1 stays a pure feature
  change"). Business logic arrives with its owning backlog item, not ad
  hoc.
- **Don't silently "fix" the accountability verdict precedence** or other
  quirks the analysis docs flag as intentional-looking legacy behavior
  (e.g. the `IsOverdue`-checked-before-promise-pending order in
  `AccountabilityRow.Verdict`) — ADR-0005 requires a golden-master capture
  and an explicit product decision before deviating, not a "senior-engineer
  cleanup."
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
  it.
- **Don't add per-status button disabling to `TaskRow`'s status controls.**
  All four buttons (Not started/In progress/Blocked/Mark done) stay
  visible and clickable regardless of the row's current status, matching
  legacy exactly — `ChangeStatusAsync`'s no-op guard is what makes a
  redundant same-status click harmless, not UI-level prevention. Adding
  `disabled` logic would be an unrequested deviation from the ported
  behavior.
- **`git.strategy: branch-per-change`'s `finalize` step auto-merges the
  feature branch into `master` locally** — it does not leave the branch
  open for a separate GitHub PR. If a real reviewable PR is wanted for a
  future change, that needs deciding *before* running `/specclaw:build`
  (or accept that `/specclaw:pr` will find head==base and a straight
  `git push` is the only option left). Confirmed a fourth time on
  `task-status-transitions`.

## Recent Decisions

1. **All four `WorkItemStatus` values are exposed as buttons on every
   `TaskRow`** ("Not started"/"In progress"/"Blocked"/"Mark done", matching
   Executive Planning Desktop's order/labels), resolving the proposal's
   open question against Manager Planner Desktop's Done-only shortcut —
   every button is the same one-line `ChangeStatusAsync` call, and a
   Done-only surface would make `Blocked`/`InProgress` permanently
   unreachable through any UI in the rebuild (task-status-transitions,
   2026-07-28).
2. **No `Reason` input and no confirmation dialog for status changes** —
   confirmed by reading both legacy call sites directly; neither ever
   supplies a `Reason` or shows a confirmation before a status change
   (unlike deletion). `StatusChange.Reason` stays in the schema, unset,
   pending a future item (task-status-transitions, 2026-07-28).
3. **`TaskRow`'s `StatusChanged` `EventCallback` wires to `ProjectDetail`'s
   existing full `RefreshAsync`, not a new lighter refresh method** — a
   status change can move a task in/out of `Done` and `Overdue`, both
   tracked by the page's summary counts, so reusing the full refresh keeps
   those counts correct immediately rather than shipping a feature that
   makes its own summary visibly stale (task-status-transitions,
   2026-07-28).
4. **`AddTaskAsync` drops the legacy `discoveredInMeetingId` parameter
   entirely, rather than porting it and always passing `null`** — nothing
   in this item's UI can supply a meeting id (no `Meeting` entity/UI
   exists yet); a future item can add it back once meeting discovery is
   actually wired (task-management, 2026-07-28).
5. **A unified "Add task" form (all `WorkItem` fields, one instance per
   project) replaces Manager Planner Desktop's per-objective coarse
   inline fast-add entirely** — not preserved as a second path
   (task-management, 2026-07-28).
