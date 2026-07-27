# Design: Scaffold the Blazor web solution (ManagerPlanner.Core + ManagerPlanner.Web)

**Change:** scaffold-blazor-solution
**Created:** 2026-07-27

## Technical Approach

Build a fresh two-project .NET 8 solution from the standard templates, then
port the legacy domain model into `ManagerPlanner.Core` and wire it into a
Blazor Server host (`ManagerPlanner.Web`):

1. `dotnet new classlib` for `ManagerPlanner.Core`; `dotnet new blazor
   --interactivity Server` (the unified .NET 8 Blazor Web App template with
   Interactive Server render mode) for `ManagerPlanner.Web`; a `.sln` tying
   both together with `Web → Core` as a project reference.
2. Port entities/enums verbatim (field-for-field) from domain-model.md's
   Entities section into `Core/Domain/`.
3. Port `PlanningRules` (+ `ValidationException`) verbatim from
   domain-model.md's Business Rules section into `Core/Validation/`.
4. Implement `PlanningDbContext` in `Core/Data/`, configuring every entity
   and every relationship/delete-behavior pair from domain-model.md's
   Relationships section via Fluent API in `OnModelCreating` — this is a
   direct transcription of documented rules, not new design.
5. Add an `IDesignTimeDbContextFactory<PlanningDbContext>` so EF Core
   tooling (`dotnet ef migrations add`, `dotnet ef database update`) can run
   against `Core` standalone, without `Web` as the active project.
6. Generate the single `InitialCreate` migration from that model.
7. Wire `Web/Program.cs`: `AddDbContextFactory<PlanningDbContext>` reading a
   SQLite connection string from `appsettings.json`, apply
   `Database.Migrate()` at startup, then a placeholder Home page that opens
   a context via the factory and confirms connectivity.

No `PlanningService` and no feature UI exist after this change — it is
infrastructure only, matching ADR-0002's sequencing intent ("item 0
(scaffold)... before item 1 [stays] a pure feature change").

## Architecture

```
ManagerPlanner.sln
├── src/ManagerPlanner.Core/            (class library, net8.0)
│   ├── ManagerPlanner.Core.csproj      (PackageReference: EFCore.Sqlite, EFCore.Design [dev-only])
│   ├── Domain/
│   │   ├── User.cs
│   │   ├── Project.cs
│   │   ├── Objective.cs
│   │   ├── WorkItem.cs
│   │   ├── ChecklistItem.cs
│   │   ├── TaskOwner.cs
│   │   ├── Meeting.cs
│   │   ├── ProgressNote.cs
│   │   ├── StatusChange.cs
│   │   └── Enums.cs                    (ProjectStatus, WorkItemStatus, MeetingType, UserRole)
│   ├── Validation/
│   │   └── PlanningRules.cs            (+ ValidationException)
│   ├── Data/
│   │   ├── PlanningDbContext.cs
│   │   └── PlanningDbContextFactory.cs (IDesignTimeDbContextFactory)
│   └── Migrations/
│       └── <timestamp>_InitialCreate.cs (+ PlanningDbContextModelSnapshot.cs)
└── src/ManagerPlanner.Web/             (Blazor Server, net8.0)
    ├── ManagerPlanner.Web.csproj       (ProjectReference: Core)
    ├── Program.cs
    ├── appsettings.json / appsettings.Development.json
    └── Components/
        ├── App.razor, Routes.razor, Layout/MainLayout.razor  (template scaffolding)
        └── Pages/Home.razor            (placeholder: confirms DB connectivity only)
```

`Web` depends on `Core`; `Core` has no dependency on `Web`. No third
project/API boundary — components (once added, starting with item 1) call
domain/service types directly, per ADR-0002's flat-service-surface guidance.

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `ManagerPlanner.sln` | Create | Solution tying Core + Web together |
| `.gitignore` | Create | Standard .NET `bin/`/`obj/` + local SQLite db file pattern |
| `src/ManagerPlanner.Core/ManagerPlanner.Core.csproj` | Create | net8.0 class library; `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design` (dev-only) |
| `src/ManagerPlanner.Core/Domain/User.cs` | Create | `Id, FullName, Email, Role (UserRole), IsActive`; nav `OwnedProjects`, `AssignedTasks`, `OwnedTasks` |
| `src/ManagerPlanner.Core/Domain/Project.cs` | Create | `Id, Name, Description?, Status (ProjectStatus), CreatedUtc, OwnerId/Owner`; nav `Objectives`, `Tasks`, `Meetings` |
| `src/ManagerPlanner.Core/Domain/Objective.cs` | Create | `Id, Title, KeyResult?, SortOrder, ProjectId/Project`; nav `Tasks` |
| `src/ManagerPlanner.Core/Domain/WorkItem.cs` | Create | `Id, Title, Description?, Status (WorkItemStatus), Deadline?, CreatedUtc, CompletedUtc?, IsDiscovered, ProjectId/Project, ObjectiveId?/Objective, AssigneeId?/Assignee, DiscoveredInMeetingId?/DiscoveredInMeeting`; nav `Notes`, `StatusHistory`, `Checklist`, `Owners` |
| `src/ManagerPlanner.Core/Domain/ChecklistItem.cs` | Create | `Id, Label, IsDone, SortOrder, CompletedUtc?, WorkItemId/WorkItem, ParentId?/Parent/Children, AssigneeId?/Assignee` |
| `src/ManagerPlanner.Core/Domain/TaskOwner.cs` | Create | Composite key `WorkItemId/WorkItem, UserId/User` |
| `src/ManagerPlanner.Core/Domain/Meeting.cs` | Create | `Id, Title, Type (MeetingType), MeetingDate, ProjectId/Project, ParticipantId?/Participant`; nav `Notes`, `DiscoveredTasks` |
| `src/ManagerPlanner.Core/Domain/ProgressNote.cs` | Create | `Id, Text, CreatedUtc, NoteDate, IsPromise, PromisedDate?, WorkItemId/WorkItem, MeetingId?/Meeting, AuthorId/Author` |
| `src/ManagerPlanner.Core/Domain/StatusChange.cs` | Create | `Id, FromStatus, ToStatus, ChangedUtc, Reason?, WorkItemId/WorkItem, ChangedById/ChangedBy` |
| `src/ManagerPlanner.Core/Domain/Enums.cs` | Create | `ProjectStatus`, `WorkItemStatus`, `MeetingType`, `UserRole` with documented int values |
| `src/ManagerPlanner.Core/Validation/PlanningRules.cs` | Create | `ValidationException` + `ValidateProjectName/ValidateTaskTitle/ValidateObjectiveTitle/ValidateChecklistLabel/ValidateNoteText/ValidateNoteDate` |
| `src/ManagerPlanner.Core/Data/PlanningDbContext.cs` | Create | `DbSet`s for all 9 entities; `OnModelCreating` with all 9 relationship/delete-behavior pairs |
| `src/ManagerPlanner.Core/Data/PlanningDbContextFactory.cs` | Create | `IDesignTimeDbContextFactory<PlanningDbContext>` for `dotnet ef` tooling |
| `src/ManagerPlanner.Core/Migrations/*` | Create | Generated `InitialCreate` migration + model snapshot |
| `src/ManagerPlanner.Web/ManagerPlanner.Web.csproj` | Create | net8.0 Blazor Server (unified template, Interactive Server); `ProjectReference` → Core |
| `src/ManagerPlanner.Web/Program.cs` | Create | `AddDbContextFactory<PlanningDbContext>`, `Database.Migrate()` at startup, Razor Components services |
| `src/ManagerPlanner.Web/appsettings.json` | Create | SQLite connection string |
| `src/ManagerPlanner.Web/Components/Pages/Home.razor` | Create | Placeholder page; confirms DB connectivity only |

## Data Model Changes

This change **introduces** the initial schema (there is no prior schema in
this repo). All 9 tables and their relationships are ported verbatim from
`domain-model.md`'s ERD — no new entities, fields, or relationships beyond
what that document already specifies:

| Relationship | Delete behavior |
|---|---|
| `User` → `Project` (owns) | `Restrict` |
| `Project` → `Objective` / `WorkItem` / `Meeting` | `Cascade` |
| `Objective` → `WorkItem` (optional) | `SetNull` |
| `User` (assignee) → `WorkItem` | `SetNull` |
| `Meeting` → `WorkItem` (discovered-in) | `SetNull` |
| `WorkItem` → `ProgressNote` / `StatusChange` / `ChecklistItem` | `Cascade` |
| `Meeting` → `ProgressNote` (optional) | `SetNull` |
| `User` (author/changed-by) → `ProgressNote` / `StatusChange` | `Restrict` |
| `ChecklistItem.Parent` (self-reference) | `Restrict` |
| `WorkItem` ↔ `User` via `TaskOwner` | `Cascade` (both FKs) |

One `InitialCreate` migration captures all of the above in one step.

## API Changes

None. This change adds no feature endpoints, Razor components with
business behavior, or API surface beyond a single static placeholder page
that reads (never writes) a connectivity check.

## Key Decisions

1. **Migrations live in `ManagerPlanner.Core`, not `.Web`** — resolves the
   proposal's open question in favor of `Core`, deviating from that
   document's tentative default. Rationale: the legacy `Core` project's
   *only* external dependency is `Microsoft.EntityFrameworkCore.Sqlite`
   (codebase-report.md); keeping the `DbContext`, provider package, and
   migrations together in `Core` matches that shape and lets `dotnet ef`
   run standalone via the design-time factory, without needing `.Web` built
   or referenced as the tooling's startup project.
2. **`IDbContextFactory<PlanningDbContext>`, not `AddDbContext`** — per
   ADR-0002's explicit warning to "be deliberate about `DbContext` lifetime
   per Blazor's guidance" for a Server circuit; a factory avoids one
   `DbContext` instance being shared/concurrently used across a circuit's
   lifetime.
3. **SQLite** — per the proposal's default and ADR-0003's own
   recommendation that it's "fine for a single-tenant pilot"; the
   connection string is externalized to `appsettings.json` so a future
   move to SQL Server/PostgreSQL is a config change, not a rewrite.
4. **.NET 8** — matches the legacy solution's target framework exactly
   (`codebase-report.md`: all four legacy projects target `net8.0`),
   avoiding an unforced version gap ahead of later fidelity comparisons.
5. **Fresh `ManagerPlanner.Core` naming**, not a verbatim mirror of
   `ExecutivePlanning.Core`'s namespace/folder layout — no legacy source
   tree exists in *this* repo to diff against directly, so there is no
   fidelity value in copying paths exactly.
6. **`PlanningService` is explicitly out of scope** — it is business logic
   that arrives with the feature items that need it (1, 3, 4, 6, 7, 8), not
   infrastructure; adding it now would blur this change's "no features yet"
   boundary from ADR-0002.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — entity field lists ("Fields:
  `Id`, `Name`, `Description?`, `Status` (`ProjectStatus`)...` for each of
  the 9 entities), the full relationships/delete-behavior list, and the
  exact `PlanningRules` constants (`MaxProjectName` 120, `NoteBackdateMonths`
  1, etc.) driving FR2–FR5.
- `.specclaw/analysis/codebase-report.md` — "`ExecutivePlanning.Core.csproj`:
  single dependency `Microsoft.EntityFrameworkCore.Sqlite`... Core's only
  external dependency" (grounds Key Decision 1), and ".NET 8 across the
  whole solution" (grounds Key Decision 4).
- `.specclaw/adr/0001-target-platform-blazor-web.md` — the Blazor platform
  decision this scaffold implements.
- `.specclaw/adr/0002-architecture-and-project-layout.md` — "`.Core` —
  reuse the legacy domain + EF Core model + rules" / "`.Web` — the Blazor
  app referencing `.Core`" (grounds the project layout) and "be deliberate
  about `DbContext` lifetime per Blazor's guidance" (grounds Key Decision 2).
- `.specclaw/adr/0003-persistence-and-schema-strategy.md` — "adopt EF Core
  Migrations from the first scaffold. Do NOT carry over `EnsureCreated()`"
  (grounds FR7/FR10) and "SQLite is fine for a single-tenant pilot" (grounds
  Key Decision 3).

## Risks & Mitigations

- **Risk:** transcribing 9 relationship/delete-behavior pairs by hand into
  Fluent API is mechanical but easy to get subtly wrong (e.g. `Cascade`
  where the source says `Restrict`), with no automated test yet to catch
  it. **Mitigation:** AC6 requires a direct code-review pass against
  domain-model.md's relationship list, one entry at a time; real
  cascade-behavior *tests* arrive with items 9/10 once deletion features
  exist to exercise them.
- **Risk:** a class-library-only `DbContext` project can be awkward for EF
  Core design-time tooling (no natural "startup project"). **Mitigation:**
  `IDesignTimeDbContextFactory<PlanningDbContext>` (FR6) makes `Core`
  self-sufficient for `dotnet ef` commands.
- **Risk:** Blazor Server `DbContext`-lifetime bugs (a context reused
  concurrently across circuit renders) are a known pitfall.
  **Mitigation:** `AddDbContextFactory` (NFR4/Key Decision 2), not a shared
  scoped `DbContext`.
- **Risk:** scope creep — since the entities already exist, it's tempting
  to also add `PlanningService` "while we're in here." **Mitigation:**
  FR12/AC7 make the exclusion an explicit, checkable acceptance criterion.
