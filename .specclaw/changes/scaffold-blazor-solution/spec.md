# Spec: Scaffold the Blazor web solution (ManagerPlanner.Core + ManagerPlanner.Web)

**Change:** scaffold-blazor-solution
**Created:** 2026-07-27
**Status:** 🟡 Draft

## Overview

This change creates the first buildable, runnable artifact in the rebuild: a
two-project .NET 8 solution (`ManagerPlanner.Core`, `ManagerPlanner.Web`) with
the domain entity model, validation rules, and EF Core persistence ported
from the legacy `ExecutivePlanning.Core` library, wired into a Blazor Server
host via dependency injection. There is **no feature UI and no
`PlanningService`** in this change — only the skeleton every later
backlog item (1–13) builds on top of, per ADR-0002's explicit sequencing
("Backlog item 0 (scaffold) should create this solution/project skeleton and
EF Core wiring before item 1... stays a pure feature change").

## Requirements

### Functional Requirements

1. **FR1 — Solution structure.** A `.sln` exists referencing two projects:
   `ManagerPlanner.Core` (class library) and `ManagerPlanner.Web` (Blazor
   Server), with `Web` holding a project reference to `Core`.
2. **FR2 — Ported entities.** `ManagerPlanner.Core` contains all nine
   entities documented in `domain-model.md`'s Entities section, with the
   exact documented fields: `User`, `Project`, `Objective`, `WorkItem`,
   `ChecklistItem`, `TaskOwner`, `Meeting`, `ProgressNote`, `StatusChange`.
3. **FR3 — Ported enums.** `ManagerPlanner.Core` contains `ProjectStatus`
   (`Active=0, OnHold=1, Completed=2, Cancelled=3`), `WorkItemStatus`
   (`NotStarted=0, InProgress=1, Blocked=2, Done=3`), `MeetingType`
   (`VideoCall=0, PhysicalMeeting=1, PhoneCall=2`), and `UserRole`
   (`Manager=0, TeamMember=1`) — matching domain-model.md's documented
   values exactly (`User.Role` requires `UserRole` even though it isn't
   named in the proposal's entity list).
4. **FR4 — Ported validation (`PlanningRules`).** `ManagerPlanner.Core`
   contains a `PlanningRules` static class (plus its `ValidationException`
   type) with the validators domain-model.md documents under Business
   Rules 1–6: `ValidateProjectName` (max 120), `ValidateTaskTitle` (max
   120), `ValidateObjectiveTitle` (max 150), `ValidateChecklistLabel` (max
   300), `ValidateNoteText` (max 2000), `ValidateNoteDate`
   (`NoteBackdateMonths = 1`, no future dates). Constants must match the
   documented numbers exactly — later features assume them.
5. **FR5 — `PlanningDbContext`.** A `DbContext` in `ManagerPlanner.Core`
   maps all nine entities and configures every relationship/delete-behavior
   pair listed in domain-model.md's Relationships section (9 relationships:
   `User→Project` Restrict; `Project→Objective/WorkItem/Meeting` Cascade;
   `Objective→WorkItem` SetNull; `User(assignee)→WorkItem` SetNull;
   `Meeting→WorkItem(discovered)` SetNull; `WorkItem→ProgressNote/
   StatusChange/ChecklistItem` Cascade; `Meeting→ProgressNote` SetNull;
   `User(author/changed-by)→ProgressNote/StatusChange` Restrict;
   `ChecklistItem.Parent` self-reference Restrict; `WorkItem↔User` via
   `TaskOwner` Cascade on both FKs).
6. **FR6 — Design-time tooling.** An `IDesignTimeDbContextFactory<PlanningDbContext>`
   exists so `dotnet ef migrations` commands run without needing
   `ManagerPlanner.Web` as the active/startup project.
7. **FR7 — Initial migration.** Exactly one EF Core migration
   (`InitialCreate`) exists, generated from the model above, representing
   the full initial schema (replacing the legacy `EnsureCreated()` pattern
   per ADR-0003).
8. **FR8 — Blazor Server host.** `ManagerPlanner.Web` is a Blazor Server
   project (per ADR-0002's recommended hosting model) referencing `.Core`.
9. **FR9 — DI wiring.** `Program.cs` registers `PlanningDbContext` via
   `AddDbContextFactory<PlanningDbContext>` (not a directly injected scoped
   `DbContext`), with the connection string read from `appsettings.json`.
10. **FR10 — Migrate on startup.** The app applies any pending migrations
    (`Database.Migrate()`) against the configured database before serving
    requests.
11. **FR11 — Placeholder page.** A single Home page renders and confirms
    database connectivity (e.g. `Database.CanConnectAsync()`), with no
    project/task/meeting/note CRUD UI.
12. **FR12 — No business-logic layer yet.** No `PlanningService` methods
    (`AddProjectAsync`, `ChangeStatusAsync`, `GetAccountabilityReportAsync`,
    etc.) are added in this change — those arrive with backlog items 1, 3,
    4, 6, 7, 8.

### Non-Functional Requirements

1. **NFR1 — Target framework.** Both projects target **.NET 8**, matching
   the legacy solution (avoids a version wrinkle when later features are
   compared against legacy behavior for fidelity per ADR-0005).
2. **NFR2 — Clean build.** `dotnet build` succeeds from a clean checkout
   with only `dotnet restore` + `dotnet build` — no manual setup steps.
3. **NFR3 — Runs.** `dotnet run --project <Web project>` starts the host
   and serves the placeholder page over HTTP without unhandled exceptions.
4. **NFR4 — DbContext lifetime.** Uses the `IDbContextFactory<T>` pattern,
   not a single injected scoped `DbContext`, per ADR-0002's Blazor Server
   circuit-lifetime warning.
5. **NFR5 — Database engine.** SQLite (per this change's Notes/defaults
   below), with the connection string externalized to `appsettings.json`
   so switching engines later is a config change, not a rewrite. The
   database file itself is not committed to source control.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors for
   both `ManagerPlanner.Core` and `ManagerPlanner.Web`.
2. **AC2** — Exactly one EF Core migration (`InitialCreate`) exists, and
   `dotnet ef migrations add` against the current model reports **no**
   further pending model changes (the migration fully represents FR2/FR3/FR5).
3. **AC3** — Running `dotnet run` against `ManagerPlanner.Web` starts the
   app, creates/migrates the configured SQLite database file, and the Home
   page responds with content confirming DB connectivity.
4. **AC4** — All 9 entities and 4 enums exist in `ManagerPlanner.Core` with
   fields matching domain-model.md's documented field lists exactly
   (verified by code review against the Entities section).
5. **AC5** — `PlanningRules` exists with the exact documented constants
   (`MaxProjectName=120`, `MaxTaskTitle=120`, `MaxObjectiveTitle=150`,
   `MaxChecklistLabel=300`, `MaxNoteText=2000`, `NoteBackdateMonths=1`,
   `PromisedDate`/no-future-date rule) — no unit tests are required yet
   (there is no `PlanningService` to exercise them against), but the
   constants themselves must match verbatim since every later feature
   assumes them.
6. **AC6** — `PlanningDbContext.OnModelCreating` configures every one of
   the 9 relationship/delete-behavior pairs listed in domain-model.md,
   verified by code review against that list (no cascade-behavior tests
   are required yet — those arrive with items 9/10, which actually delete
   rows).
7. **AC7** — No `PlanningService`, feature UI, or business-logic method
   exists anywhere in the diff (explicit scope check against FR12).

## Edge Cases

- **First run, no DB file yet:** `Database.Migrate()` must create the
  SQLite file/schema from nothing — this is the only "seeding" behavior in
  scope; there is no sample-data seeding (`DbSeeder`) in this change
  (that's backlog item 11).
- **Second run, DB already migrated:** `Database.Migrate()` must be a
  clean no-op (no exception, no duplicate schema objects).
- **Design-time tooling without a running host:** `dotnet ef migrations
  add`/`dotnet ef database update` must work directly against
  `ManagerPlanner.Core` via the `IDesignTimeDbContextFactory`, without
  needing `ManagerPlanner.Web` built or running first.

## Dependencies

- **None** — this is backlog item 0, the first item in the rebuild
  sequence; nothing in the repo precedes it.
- **Blocks everything else.** Every later backlog item (1–13) depends on
  this scaffold existing (solution structure, entity model, and a migrated
  database to build features against).

## Notes

This change adopts the following defaults from the proposal's Open
Questions, since no override was given before `/specclaw:plan` was run:

- **Database engine:** SQLite (ADR-0003 leaves this an explicit open
  `DECIDE`; SQLite matches the legacy app and is "fine for a single-tenant
  pilot" per that ADR's recommendation). If the team later confirms a
  server database (SQL Server/PostgreSQL) is required for a genuine
  multi-user deployment, that is a follow-up change to the connection
  string/provider, not a redo of this scaffold.
- **.NET version:** .NET 8 (current LTS; matches the legacy solution).
- **Naming:** fresh `ManagerPlanner.Core` namespaces/folder layout (not a
  verbatim mirror of legacy `ExecutivePlanning.Core` paths), since no
  legacy source tree exists in this repo to diff against directly.
- **Migrations location:** resolved in `design.md` (see Key Decisions) —
  co-located inside `ManagerPlanner.Core` alongside the `DbContext`, not in
  `ManagerPlanner.Web`.

No golden-master capture (ADR-0005) applies to this change — there is no
business logic here to verify against legacy behavior yet; ADR-0005's
capture step becomes relevant starting with backlog item 1.
