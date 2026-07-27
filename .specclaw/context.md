# Project Context

_Last updated: 2026-07-27 — after "scaffold-blazor-solution"._

## Architecture Overview

A two-project .NET 8 solution (`ManagerPlanner.sln`) rebuilding the legacy
Avalonia desktop app (`ExecutivePlanning.Core` + two desktop shells) as a
single Blazor web app:

- **`src/ManagerPlanner.Core`** (class library) — the domain/persistence
  layer. Holds entities (`Domain/`), validation (`Validation/PlanningRules.cs`),
  the EF Core `DbContext` + migrations (`Data/`, `Migrations/`). No business
  logic yet — no `PlanningService` exists. This project's only dependency
  is `Microsoft.EntityFrameworkCore.Sqlite` (+ `.Design`, dev-only), matching
  the legacy `ExecutivePlanning.Core.csproj`'s shape.
- **`src/ManagerPlanner.Web`** (Blazor Server, unified .NET 8 "Blazor Web
  App" template with Interactive Server render mode) — references `.Core`
  directly. No API/DTO boundary between them (components call `.Core` types
  directly), per ADR-0002's flat-service-surface guidance.

Data flow: `Program.cs` registers `PlanningDbContext` via
`AddDbContextFactory<T>`, applies pending EF Core migrations at startup
(`Database.Migrate()`), then serves Razor components. No feature UI exists
yet — only a placeholder Home page confirming DB connectivity.

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
  read the equivalent legacy file directly, not just the doc excerpt.

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

1. **Migrations + EF Core Sqlite/Design packages live in `ManagerPlanner.Core`, not `.Web`** — mirrors the legacy `Core` project's single-external-dependency shape and keeps `dotnet ef` tooling self-sufficient (scaffold-blazor-solution, 2026-07-27).
2. **Always ground-truth entity/validation ports against the real legacy source** at `C:\Learnings\Projects\manager-planner`, not just `.specclaw/analysis/*.md` summaries — caught a real `User.OwnedTasks` type bug and several missing load-bearing entity defaults this way (scaffold-blazor-solution, 2026-07-27).
3. **SQLite + EF Core Migrations (not `EnsureCreated()`)** adopted from the first scaffold, per ADR-0003 (scaffold-blazor-solution, 2026-07-27).
