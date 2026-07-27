# Tasks: Scaffold the Blazor web solution (ManagerPlanner.Core + ManagerPlanner.Web)

**Change:** scaffold-blazor-solution
**Created:** 2026-07-27
**Total Tasks:** 7

## Summary

Seven tasks across three waves: (1) stand up the solution/project skeleton
and port the domain entities/enums/validation rules, (2) build the EF Core
persistence layer and its initial migration, (3) wire the Blazor Server host
and prove the whole thing builds, migrates, and runs. No task adds
`PlanningService` or any feature UI — that is explicitly out of scope for
this change (spec.md FR12/AC7).

## Tasks

### Wave 1 — Solution skeleton and domain model

- [x] `T1` — Scaffold solution and project structure
  - Files: `ManagerPlanner.sln`, `.gitignore`, `src/ManagerPlanner.Core/ManagerPlanner.Core.csproj`, `src/ManagerPlanner.Web/ManagerPlanner.Web.csproj`
  - Estimate: small
  - Depends: none
  - Notes: `dotnet new classlib` for Core (net8.0); `dotnet new blazor --interactivity Server` for Web (net8.0, unified Blazor Web App template); `dotnet new sln` + add both projects; `Web` gets a `ProjectReference` to `Core`. Add `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets="all"`) package references to `Core` (design.md Key Decision 1 — migrations/tooling live in Core, not Web). `.gitignore` covers `bin/`/`obj/` plus the local SQLite db file (e.g. `*.db`).

- [x] `T2` — Port domain entities and enums
  - Files: `src/ManagerPlanner.Core/Domain/User.cs`, `Project.cs`, `Objective.cs`, `WorkItem.cs`, `ChecklistItem.cs`, `TaskOwner.cs`, `Meeting.cs`, `ProgressNote.cs`, `StatusChange.cs`, `Enums.cs`
  - Estimate: medium
  - Depends: `T1`
  - Notes: field-for-field per spec.md FR2/FR3 and design.md's File Changes Map — no fields beyond what's documented in domain-model.md's Entities section. `Enums.cs` holds `ProjectStatus`, `WorkItemStatus`, `MeetingType`, `UserRole` with the exact documented integer values.

- [x] `T3` — Port `PlanningRules` validation
  - Files: `src/ManagerPlanner.Core/Validation/PlanningRules.cs`
  - Estimate: small
  - Depends: `T1`
  - Notes: `ValidationException` + the six validators from spec.md FR4, with the exact constants (`MaxProjectName=120`, `MaxTaskTitle=120`, `MaxObjectiveTitle=150`, `MaxChecklistLabel=300`, `MaxNoteText=2000`, `NoteBackdateMonths=1`). No entity dependency, so this can build alongside T2.

### Wave 2 — EF Core persistence

- [x] `T4` — Implement `PlanningDbContext` and design-time factory
  - Files: `src/ManagerPlanner.Core/Data/PlanningDbContext.cs`, `src/ManagerPlanner.Core/Data/PlanningDbContextFactory.cs`
  - Estimate: medium
  - Depends: `T2`
  - Notes: `DbSet<T>` for all 9 entities; `OnModelCreating` configures every relationship/delete-behavior pair from design.md's Data Model Changes table (9 pairs — transcribe one at a time against that table, don't improvise). `PlanningDbContextFactory : IDesignTimeDbContextFactory<PlanningDbContext>` with a fixed dev SQLite connection string, so `dotnet ef` commands work without `Web` built.

- [x] `T5` — Generate initial EF Core migration
  - Files: `src/ManagerPlanner.Core/Migrations/*_InitialCreate.cs`, `src/ManagerPlanner.Core/Migrations/PlanningDbContextModelSnapshot.cs`
  - Estimate: small
  - Depends: `T4`
  - Notes: `dotnet ef migrations add InitialCreate -p src/ManagerPlanner.Core`. Verify no further pending model changes are reported afterward (spec.md AC2).

### Wave 3 — Web host wiring and verification

- [x] `T6` — Wire Blazor Server DI, config, and startup migration
  - Files: `src/ManagerPlanner.Web/Program.cs`, `src/ManagerPlanner.Web/appsettings.json`, `src/ManagerPlanner.Web/appsettings.Development.json`
  - Estimate: small
  - Depends: `T5`
  - Notes: `AddDbContextFactory<PlanningDbContext>(...)` (not `AddDbContext`, per design.md Key Decision 2) reading the SQLite connection string from config; call `Database.Migrate()` via a factory-created context before `app.Run()`.

- [x] `T7` — Placeholder Home page + build/run verification
  - Files: `src/ManagerPlanner.Web/Components/Pages/Home.razor`
  - Estimate: small
  - Depends: `T6`
  - Notes: page injects `IDbContextFactory<PlanningDbContext>`, opens a context, calls `Database.CanConnectAsync()`, and renders a simple confirmation — no CRUD UI. Close out the wave by running `dotnet build` at the solution root (AC1) and `dotnet run --project src/ManagerPlanner.Web` (AC3), confirming the SQLite file is created/migrated and the Home page loads without exceptions.

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
