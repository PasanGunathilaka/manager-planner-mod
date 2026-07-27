# Spec: Project management — create, browse, switch, and summarize projects

**Change:** project-management
**Created:** 2026-07-27
**Status:** 🟡 Draft

## Overview

This change adds the first slice of business logic (`PlanningService`) and
the first feature UI (`/projects`, `/projects/{id}`) to the scaffold from
`scaffold-blazor-solution`. It ports exactly three `PlanningService` methods
from the real legacy source (`GetProjectsAsync`, `AddProjectAsync`,
`GetProjectSummaryAsync`), adds one new helper needed only because this app
has no authentication yet (`GetCurrentManagerIdAsync`), and a matching
startup bootstrap that guarantees a `User` with `Role = Manager` always
exists. No other entity's business logic (Objective, WorkItem, Meeting,
ProgressNote, Accountability) is touched — those arrive with their own
backlog items.

## Requirements

### Functional Requirements

1. **FR1 — `GetProjectsAsync()`.** Returns all projects, newest-first
   (`OrderByDescending(p => p.CreatedUtc)`), with `Owner` eagerly loaded
   (`Include(p => p.Owner)`) — ported verbatim from the legacy
   `Services/PlanningService.cs`. No pagination.
2. **FR2 — `AddProjectAsync(name, description, ownerId)`.** Validates
   `name` via the already-ported `PlanningRules.ValidateProjectName`,
   trims `name`/`description`, creates a `Project` and saves it.
   `CreatedUtc`/`Status` are **not** set explicitly — they come from
   `Project`'s own entity-level defaults, exactly matching legacy.
3. **FR3 — `GetProjectSummaryAsync(projectId)`.** Returns a `ProjectSummary`
   (`Core/Services/Reports.cs`, not EF-mapped): `ProjectId`, `ProjectName`
   (empty string if the project doesn't exist — matches legacy's
   `project?.Name ?? string.Empty`, not an exception), `TotalTasks`,
   `Done`, `InProgress`, `Blocked`, `NotStarted`, `Overdue`
   (`Deadline.HasValue && Deadline.Value < DateTime.UtcNow && Status !=
   Done`), `Discovered` (`IsDiscovered`), and a computed `PercentComplete`
   = `TotalTasks == 0 ? 0 : Math.Round(100.0 * Done / TotalTasks, 1)` —
   this exact formula is read directly from the legacy `Services/Reports.cs`.
4. **FR4 — `GetCurrentManagerIdAsync()` (new, no legacy equivalent).**
   Returns the `Id` of the single bootstrapped `User` with `Role =
   Manager`. Needed because this app has neither authentication nor
   `DbSeeder`-style sample data yet, so there is no other source for
   `AddProjectAsync`'s required `ownerId`.
5. **FR5 — Startup Manager bootstrap.** On app startup, after pending
   migrations are applied, if no `User` with `Role = Manager` exists,
   create exactly one (fixed name/email) and save. Idempotent — a second
   startup against an already-bootstrapped database creates no additional
   row.
6. **FR6 — `/projects` page.** Lists all projects (Name, Description) via
   `GetProjectsAsync`; each row links to `/projects/{id}`. A create form
   (Name required, Description optional) calls `GetCurrentManagerIdAsync()`
   then `AddProjectAsync(...)`; on success, the list refreshes to include
   the new project; on a thrown `ValidationException`, its `Message` is
   shown inline and no project is created.
7. **FR7 — `/projects/{id:int}` page.** Shows the project's summary counts
   (Total/Done/In progress/Blocked/Not started/Overdue/% complete) via
   `GetProjectSummaryAsync(id)`, plus a "Refresh" button that re-queries it
   explicitly (no auto-polling). If the returned summary indicates the
   project doesn't exist (`ProjectName` empty and `TotalTasks == 0`), the
   page shows "Project not found" instead of a confusing all-zero
   dashboard — see Edge Cases.
8. **FR8 — Navigation.** A nav link to `/projects` is added to the app's
   (currently bare) `MainLayout`. "Switching the active project" is URL
   navigation between `/projects/{id}` rows — no separate "current
   project" session state is introduced.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** `PlanningService` is registered
   **Scoped** and constructed with `IDbContextFactory<PlanningDbContext>`
   (never a directly injected `PlanningDbContext`) — each method
   opens/disposes its own short-lived context, consistent with the
   pattern established in `scaffold-blazor-solution` for Blazor Server.
2. **NFR2 — Interactivity.** Both new pages use `@rendermode
   InteractiveServer` explicitly — the Blazor Web App template's default
   static server rendering does not process `@onclick` handlers, and both
   pages need one (Add project, Refresh).
3. **NFR3 — Scope discipline.** No `PlanningService` method beyond the
   four listed above (FR1–FR4) exists after this change. Objective,
   WorkItem, Meeting, ProgressNote, Accountability, and deletion logic are
   explicitly out of scope (items 2–10).
4. **NFR4 — Conventions.** Follows the .NET 8 / nullable-enabled
   conventions already established (`.specclaw/context.md`).

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — Starting the app against an empty database creates exactly
   one `User` with `Role = Manager`; starting it again against the
   already-bootstrapped database creates no second Manager row.
3. **AC3** — Submitting the `/projects` create form with a valid name
   persists a new `Project` row owned by the bootstrapped Manager, with a
   non-default `CreatedUtc` and `Status == Active`, and the page's list
   updates to show it without a manual reload.
4. **AC4** — Submitting an empty/whitespace-only name, or a name over 120
   characters, shows the `PlanningRules` validation message inline and
   creates no `Project` row.
5. **AC5** — Visiting `/projects/{id}` for a project with a known mix of
   task statuses shows counts matching that data and a `PercentComplete`
   equal to `Math.Round(100.0 * Done / TotalTasks, 1)` for at least one
   non-trivial (non-zero, non-100%) case.
6. **AC6** — Visiting `/projects/{id}` for a project id that does not
   exist shows "Project not found" rather than an all-zero dashboard.
7. **AC7** — After the underlying task data for a project changes (e.g. a
   `WorkItem`'s status is updated directly), clicking "Refresh" on that
   project's `/projects/{id}` page updates the displayed counts to match
   — confirming the button re-queries rather than showing stale/cached
   data.
8. **AC8** — No `PlanningService` method beyond `GetProjectsAsync`,
   `AddProjectAsync`, `GetProjectSummaryAsync`, and
   `GetCurrentManagerIdAsync` exists anywhere in the diff (scope check
   against NFR3).

## Edge Cases

- **Empty database, first run.** `/projects` shows an empty list (not an
  error); creating the first project still works via the bootstrapped
  Manager.
- **Nonexistent project id in the URL.** Unlike the legacy desktop apps
  (which could only ever select a project from a live list), this is a
  URL-addressable web page — a user can type any integer. Handled per FR7
  ("Project not found"), a genuinely new edge case the web port
  introduces that the legacy app never had to handle.
- **Overlong or whitespace-only project name.** Rejected per FR2/AC4 —
  no `Project` row is created, and the existing project list is
  unaffected.
- **Unconstrained `Description`.** Legacy has no validator for
  `Description` (only `ValidateProjectName` exists for `Project`) — do
  not invent a length limit; any string (including empty/null) is
  accepted, matching legacy's absence of a check.
- **Verifying the "Refresh" button (AC7) requires task data that no UI
  can create yet** (task creation is item 3) — verification will insert/
  update a `WorkItem` row directly (EF Core or a scratch script), not
  through any UI, and confirm the page's Refresh button picks it up.

## Dependencies

- **Depends on:** `scaffold-blazor-solution` (item 0) — the entity model,
  `PlanningDbContext`, and running host it produced.
- **Blocks:** every subsequent backlog item (2–11) that needs a `Project`
  to exist and be selectable before its own feature can be built or
  tested.

## Notes

This item introduces the first genuinely new (non-legacy-port) pieces:
`GetCurrentManagerIdAsync` and the startup Manager bootstrap — both exist
solely to stand in for the authentication/multi-user decision ADR-0001
defers as a future question. If/when a real auth ADR is written, this
bootstrap is expected to be replaced, not extended.
