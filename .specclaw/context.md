# Project Context

_Last updated: 2026-07-27 — after "project-management"._

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
refresh) — the first slice of the legacy app's Project management surface.
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
  one method per legacy operation, each opening/disposing its own
  `PlanningDbContext` via the injected `IDbContextFactory` (see Key
  Patterns). Read-model DTOs (e.g. `ProjectSummary`) live alongside it in
  `Services/Reports.cs`, matching the legacy file split.
- Feature pages needing interactivity (button clicks, form submits) MUST
  declare `@rendermode InteractiveServer` explicitly — the Blazor Web App
  template's default static server rendering does not process `@onclick`
  handlers at all; forgetting this makes a page look right but silently do
  nothing on click.

## Key Patterns

- **`IDbContextFactory<PlanningDbContext>` via `AddDbContextFactory`, never
  `AddDbContext`** — required for Blazor Server to avoid one `DbContext`
  instance being shared/reused unsafely across a circuit's concurrent
  renders (ADR-0002). Every future component that touches the database
  should inject the factory and create/dispose a short-lived context per
  operation, not hold a long-lived injected `DbContext`.
- **EF Core migrations live inside `ManagerPlanner.Core`**, not `.Web` —
  via `PlanningDbContextFactory : IDesignTimeDbContextFactory<PlanningDbContext>`
  in `Core/Data/`. This lets `dotnet ef migrations add`/`database update`
  run standalone against `Core` without needing `.Web` built or referenced
  as the tooling's startup project.
- **Ground-truth against the real legacy source before trusting doc
  summaries or agent output.** The actual legacy repo is checked out at
  `C:\Learnings\Projects\manager-planner` (sibling directory) —
  `src/ExecutivePlanning.Core/{Domain,Data,Services}`. The
  `.specclaw/analysis/*.md` docs are prose summaries of that source, not a
  substitute for it; a coding agent working from the docs alone mistyped
  `User.OwnedTasks` and missed several load-bearing entity defaults, caught
  only by diffing against the real files. For every future backlog item,
  read the equivalent legacy file directly, not just the doc excerpt. This
  paid off again for `project-management`: `ProjectSummary.PercentComplete`'s
  exact rounding formula (`Math.Round(100.0 * Done / TotalTasks, 1)`) came
  straight from the real `Services/Reports.cs`, resolving a "golden-master
  needed" gap the rebuild-backlog had flagged as unresolved.
- **`GetCurrentManagerIdAsync()` + a startup Manager-user bootstrap stand in
  for authentication**, which doesn't exist yet. `Program.cs` guarantees
  exactly one `User` with `Role = Manager` on first startup; any feature
  needing an "owner"/"current user" calls `PlanningService.GetCurrentManagerIdAsync()`
  rather than assuming a signed-in user. Expected to be replaced, not
  extended, once a real auth ADR exists (ADR-0001 defers this).
- **Testing Blazor Server pages via claude-in-chrome:** use the `form_input`
  tool for text fields, never `computer.type` (simulated keystrokes raced
  against the SignalR round-trip and corrupted values during
  `project-management`'s verification). Always call `read_page` immediately
  before a click with no intervening tool calls — refs go stale across
  Blazor's async re-renders, and a stale-ref click can make a genuinely
  working feature (e.g. a "Refresh" button) look broken. If a click seems
  to do nothing, diff server-log query counts before/after before
  concluding the feature is broken.

## Technology Decisions

- **Blazor Server** (not WASM or a separate Web API), per ADR-0002 —
  maximizes reuse of `.Core`, no API/DTO boundary the legacy app never had.
- **SQLite**, per ADR-0003's recommendation for a single-tenant pilot;
  connection string lives in `appsettings.json` (`ConnectionStrings:PlanningDatabase`)
  so switching to SQL Server/PostgreSQL later is a config change.
- **EF Core Migrations from the first scaffold** — explicitly replaces the
  legacy `PlanningDbContextFactory.Create`'s `EnsureCreated()` (ADR-0003).
  One migration exists so far: `InitialCreate`.
- **.NET 8** — matches the legacy solution's target framework exactly, to
  avoid a version gap ahead of future fidelity comparisons.

## Constraints

- **Do not add `PlanningService` or any feature UI casually** — the
  scaffold is deliberately infrastructure-only (ADR-0002's sequencing
  intent: "item 0 (scaffold)... before item 1 stays a pure feature
  change"). Business logic (`AddProjectAsync`, `ChangeStatusAsync`,
  `GetAccountabilityReportAsync`, etc.) arrives with its owning backlog
  item, not ad hoc.
- **Don't silently "fix" the accountability verdict precedence** or other
  quirks the analysis docs flag as intentional-looking legacy behavior
  (e.g. the `IsOverdue`-checked-before-promise-pending order in
  `AccountabilityRow.Verdict`) — ADR-0005 requires a golden-master capture
  and an explicit product decision before deviating, not a "senior-engineer
  cleanup."
- **`git.strategy: branch-per-change`'s `finalize` step auto-merges the
  feature branch into `master` locally** — it does not leave the branch
  open for a separate GitHub PR. If a real reviewable PR is wanted for a
  future change, that needs deciding *before* running `/specclaw:build`
  (or accept that `/specclaw:pr` will find head==base and a straight
  `git push` is the only option left).

## Recent Decisions

1. **`PlanningService` methods take `IDbContextFactory<PlanningDbContext>`, not a direct `PlanningDbContext`** — deviates from the legacy constructor signature to preserve the Blazor Server DbContext-lifetime pattern; each method opens/disposes its own short-lived context (project-management, 2026-07-27).
2. **`GetCurrentManagerIdAsync()` + startup Manager-user bootstrap** added as a deliberate, temporary stand-in for auth/multi-user support that doesn't exist yet — not a legacy port (project-management, 2026-07-27).
3. **Migrations + EF Core Sqlite/Design packages live in `ManagerPlanner.Core`, not `.Web`** — mirrors the legacy `Core` project's single-external-dependency shape and keeps `dotnet ef` tooling self-sufficient (scaffold-blazor-solution, 2026-07-27).
4. **Always ground-truth entity/validation/business-logic ports against the real legacy source** at `C:\Learnings\Projects\manager-planner`, not just `.specclaw/analysis/*.md` summaries — caught a real `User.OwnedTasks` type bug in item 0, and confirmed the exact `PercentComplete` rounding formula from source in item 1 (scaffold-blazor-solution / project-management, 2026-07-27).
5. **SQLite + EF Core Migrations (not `EnsureCreated()`)** adopted from the first scaffold, per ADR-0003 (scaffold-blazor-solution, 2026-07-27).
