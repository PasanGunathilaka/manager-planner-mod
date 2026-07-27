# Proposal: Scaffold the Blazor web solution (ManagerPlanner.Core + ManagerPlanner.Web)

**Created:** 2026-07-27
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

The repository currently contains only planning/analysis artifacts under
`.specclaw/` — there is no .NET solution, project, or buildable code yet.
ADR-0001 commits to a Blazor web rebuild; ADR-0002 decides the project layout
(`ManagerPlanner.Core` + `ManagerPlanner.Web`, Blazor Server) and warns that
`.Core` should carry the reused domain/EF model so validation rules "can't
drift between old and new"; ADR-0003 requires **EF Core Migrations from day
one**, explicitly rejecting the legacy app's `EnsureCreated()` dead end
(`PlanningDbContextFactory.Create` only ever calls `EnsureCreated()`, with no
migration history to observe or carry forward).

Every subsequent rebuild-backlog item — starting with item 1 (Project
management) — needs a running host and a persisted, migrated schema to build
features against. Without this scaffold, item 1 would have to also invent
solution structure, DI wiring, and migration tooling, mixing infrastructure
concerns into what should be "a pure feature change." ADR-0002's Consequences
section says this directly: *"Backlog item 0 (scaffold) should create this
solution/project skeleton and EF Core wiring before item 1... stays a pure
feature change."*

## Proposed Solution

_What are we building? High-level approach._

Create a minimal, running two-project .NET solution with **no feature UI
yet** — just a skeleton that builds, runs, and can round-trip a query
against a migrated database.

1. **`ManagerPlanner.Core`** (class library) — ported from the legacy
   `ExecutivePlanning.Core` domain, per `domain-model.md`:
   - **Entities:** `User`, `Project`, `Objective`, `WorkItem`,
     `ChecklistItem`, `TaskOwner`, `Meeting`, `ProgressNote`, `StatusChange`.
   - **Enums:** `ProjectStatus`, `WorkItemStatus`, `MeetingType`.
   - **`PlanningRules`** — the validation constants/methods documented in
     domain-model.md's Business Rules (`ValidateProjectName`,
     `ValidateObjectiveTitle`, `ValidateTaskTitle`, `ValidateChecklistLabel`,
     `ValidateNoteText`, `ValidateNoteDate`, etc.), ported as-is so validation
     stays identical to the legacy app.
   - **`PlanningDbContext`** — an EF Core `DbContext` mapping every
     entity/relationship/cascade behavior from domain-model.md's ERD
     (`Cascade`/`Restrict`/`SetNull` per relationship, exactly as documented),
     plus a design-time `IDesignTimeDbContextFactory` for `dotnet ef
     migrations` tooling.
   - **No `PlanningService` methods yet** (`AddProjectAsync`,
     `ChangeStatusAsync`, `GetAccountabilityReportAsync`, etc.) — those are
     feature logic and arrive with their owning backlog items (1, 3, 4, ...),
     not this scaffold.

2. **`ManagerPlanner.Web`** (Blazor Server, per ADR-0002's recommended
   hosting model) — references `.Core`:
   - `Program.cs` wiring: `AddDbContextFactory<PlanningDbContext>` (a
     factory, not a single injected scoped `DbContext` — deliberately
     chosen per ADR-0002's warning to "be deliberate about `DbContext`
     lifetime per Blazor's guidance" for a Server circuit), plus standard
     Blazor Server services.
   - One placeholder "Home" page that proves the app boots and can execute a
     trivial query (e.g. `Database.CanConnectAsync()` or a count) against the
     database — nothing else.
   - `appsettings.json` connection string.

3. **EF Core Migrations** (ADR-0003) — a single initial migration
   (`InitialCreate`) generated from the Core model, applied via
   `Database.Migrate()` on startup. This is the schema-strategy call ADR-0003
   requires: it replaces `EnsureCreated()`, not reproduces it.

## Scope

### In Scope
- New .NET solution file
- New `ManagerPlanner.Core` class library project: entities, enums,
  `PlanningRules`, `PlanningDbContext`, design-time DbContext factory
- New `ManagerPlanner.Web` Blazor Server project referencing `.Core`
- DI wiring in `Program.cs` (`DbContextFactory`, Blazor Server services)
- Initial EF Core migration, applied via `Database.Migrate()` at startup
- One placeholder page proving the app builds, runs, and reaches the database
- `.gitignore` for .NET build artifacts (`bin/`, `obj/`)

### Out of Scope
- `PlanningService` (business/CRUD operations) — arrives with backlog items
  1, 3, 4, etc.
- Any project/task/meeting/note feature UI — items 1–13
- Sample-data seeding (`DbSeeder`) — item 11
- Authentication, per-user data scoping, concurrency — ADR-0001 flags these
  as open future questions, not committed here
- MDI-shell-to-web navigation shell (ADR-0004) — item 12's replacement, not
  this scaffold
- Legacy data import from existing SQLite files — ADR-0003 flags this as a
  separate ADR + backlog item if ever needed

## Impact

- **Files affected:** ~15–20 (estimated) — solution file, ~9 entity classes,
  2–3 enum files, `PlanningRules`, `PlanningDbContext`, DbContext factory,
  `Program.cs`, `appsettings.json`, generated migration files, `.gitignore`
- **Complexity:** small–medium (mostly mechanical porting + config, but the
  relationship/cascade mapping must be exact — it's read by every later
  backlog item's delete/cascade tests)
- **Risk:** low–medium (no user-facing behavior yet; the main risk is
  getting FK/cascade behavior subtly wrong in a way that only surfaces once
  a later item exercises deletes)

## Open Questions

1. **Database engine.** ADR-0003 leaves this an open `DECIDE`: keep SQLite
   (matches legacy, simplest for a single-tenant pilot) or move to SQL
   Server/PostgreSQL for a multi-user web deployment? This proposal defaults
   to **SQLite** unless told otherwise — ADR-0001 notes multi-user concerns
   are still an open future question, not yet committed.
2. **Where migrations live.** Co-located inside `ManagerPlanner.Web` (default
   assumed here, since `.Core` has no need for the provider package), or a
   separate `ManagerPlanner.Migrations`/infrastructure project?
3. **Target .NET version.** Not decided in any ADR read so far. Defaulting to
   **.NET 8** (current LTS, and the version ADR-0002 names for the "unified
   Blazor Web App" hosting option) unless the team wants a newer release.
4. **Namespace/folder fidelity.** Should ported entities/`PlanningRules`
   mirror the legacy `ExecutivePlanning.Core` namespace and file layout
   exactly (to ease side-by-side diffing during ADR-0005 fidelity
   verification), or adopt fresh `ManagerPlanner.Core` naming throughout?
   Defaulting to fresh naming under the new project, since no legacy source
   tree is present in this repo to diff against directly.

---

**To proceed:** Review this proposal and approve to begin planning.
