# Proposal: Project management — create, browse, switch, and summarize projects

**Created:** 2026-07-27
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

The scaffold change (backlog item 0) produced a running Blazor Server app
with a migrated database and a ported entity model — but **zero features**.
`ManagerPlanner.Core` has no `PlanningService` at all yet, and
`ManagerPlanner.Web` has only a placeholder Home page.

`Project` is the hierarchy root of the whole domain — domain-model.md's ERD
shows `PROJECT ||--o{ OBJECTIVE`, `PROJECT ||--o{ WORKITEM`, and
`PROJECT ||--o{ MEETING` all originating from it. Rebuild-backlog.md
sequences item 1 first for exactly this reason: nothing else (objectives,
tasks, meetings, notes, accountability) can be built or meaningfully tested
until a Project exists to hang it off. Rebuild-backlog item 1 also merges
six near-identical legacy UI bullets — switch/refresh/summarize/create
(Executive Planning Desktop) and browse/create (Manager Planner Desktop) —
into one feature, since architecture.md confirms both desktop ViewModels
"call `_service.*` directly" against the same `PlanningService` methods,
with "no repository/abstraction layer" in between.

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService`** (new — the first slice of it, in
   `ManagerPlanner.Core`) with exactly the three methods this item needs,
   ported from the legacy `Services/PlanningService.cs`:
   - `GetProjectsAsync()` — all projects, newest-first (`OrderByDescending(p => p.CreatedUtc)`, matching the legacy query exactly), no pagination — same flat-list simplicity as both legacy UIs.
   - `AddProjectAsync(name, description, ownerId)` — validated via the
     already-ported `PlanningRules.ValidateProjectName` (item 0), trims
     `name`/`description`. `CreatedUtc`/`Status` are **not** set here —
     they come from `Project`'s own entity-level defaults (`DateTime.UtcNow`
     / `ProjectStatus.Active`), exactly matching the legacy code's behavior.
   - `GetProjectSummaryAsync(projectId)` — a computed, non-EF-mapped
     `ProjectSummary` read-model: `TotalTasks`, `Done`, `InProgress`,
     `Blocked`, `NotStarted`, `Overdue` (`Deadline.HasValue && Deadline <
     now && Status != Done`), `Discovered`, and `PercentComplete`. **The
     exact rounding formula, read directly from the legacy
     `Services/Reports.cs`** (resolving rebuild-backlog item 1's flagged
     "golden-master needed" question): `TotalTasks == 0 ? 0 :
     Math.Round(100.0 * Done / TotalTasks, 1)` — i.e. .NET's default
     (banker's) rounding to **one decimal place**.

2. **Two Blazor pages**, replacing the legacy windows/tabs per ADR-0004's
   web-native-navigation direction (routed pages instead of MDI windows):
   - **`/projects`** — browse all projects (Name + Description, per Manager
     Planner Desktop's ListBox) and create a new one (Name + Description
     fields, validated), matching Executive Planning Desktop's + Manager
     Planner Desktop's "Add project" affordances.
   - **`/projects/{id}`** — the project detail/dashboard: the summary
     counts (Total/Done/In progress/Blocked/Not started/Overdue/% complete)
     via `GetProjectSummaryAsync`, plus a "Refresh" button that re-queries
     it explicitly (preserving the legacy's manual-refresh gesture rather
     than silently auto-polling).

   "Switching the active project" becomes navigating to a different
   project's URL/row — no separate "current project" session state is
   introduced; the URL *is* the selection. This detail page is also where
   later items (2 Objectives, 3 Tasks, 6 Meetings, 7 Notes, 8
   Accountability) will attach their own sections — this proposal builds
   only the Project-level parts.

## Scope

### In Scope
- `PlanningService` in `ManagerPlanner.Core`, with exactly
  `GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`
- `ProjectSummary` DTO (not EF-mapped), field-for-field matching legacy
  `Reports.cs`, including the exact `PercentComplete` formula above
- DI registration of `PlanningService` in `Program.cs`
- `/projects` page (browse + create)
- `/projects/{id}` page (summary + refresh)
- A nav link to `/projects` in the app layout
- A minimal startup bootstrap ensuring at least one `User` with
  `Role = Manager` exists, so `AddProjectAsync`'s required `OwnerId` is
  always satisfiable — see Open Questions; this is **not** full
  sample-data seeding (that's item 11)

### Out of Scope
- Authentication, multi-user support, or any User-management UI (ADR-0001
  flags these as open future questions, not committed here)
- Objective/WorkItem/Meeting/Note/Accountability features (items 2–8)
- Project deletion (item 10)
- Changing a project's `Status` (`OnHold`/`Completed`/`Cancelled`) — no
  legacy UI exercises this; rebuild-backlog item 1 explicitly calls this a
  "deliberate scope call, not a defect to silently fix," so it stays
  dormant here too
- Full sample-data seeding / `DbSeeder` (item 11)
- Broader golden-master test-harness setup (ADR-0005) beyond the
  `PercentComplete` formula confirmed above

## Impact

- **Files affected:** ~7–9 (estimated) — `PlanningService.cs`,
  `ProjectSummary`/`Reports.cs`-equivalent in `Core`; `Projects.razor`,
  `ProjectDetail.razor` (or similar) in `Web`; a small startup-bootstrap
  addition to `Program.cs`; a nav-link edit in the layout
- **Complexity:** small–medium (mostly a direct, now-verified port; the
  owner-bootstrap question below is the only real design decision)
- **Risk:** low — the business logic and its exact formulas are confirmed
  against the real legacy source (`C:\Learnings\Projects\manager-planner`),
  not just doc summaries; main risk is getting the owner-bootstrap
  behavior wrong (see below)

## Open Questions

1. **How is "the current owner" determined without authentication?**
   `AddProjectAsync` requires a non-nullable `ownerId` (FK `Restrict` —
   domain-model.md: "a Manager cannot be deleted while still owning
   projects"), but there's no login and no seeded `User` data yet (that's
   item 11). The legacy apps sidestep this by picking
   `users.FirstOrDefault(u => u.Role == UserRole.Manager)?.Id` at startup —
   "despite the data model allowing multiple Manager rows, the running app
   behaves as single-manager software" (domain-model.md). **Recommended:**
   a minimal startup bootstrap that creates exactly one `User` (`Role =
   Manager`) if none exists yet, and `AddProjectAsync` always uses that
   Manager's `Id` — preserving the legacy's single-manager assumption
   without building auth or full seeding now. Flag if you'd rather block
   this item on item 11's seeding, or on a real auth decision instead.
2. **Project list ordering.** `GetProjectsAsync` already orders
   newest-first in the shared legacy service (both desktop UIs inherit
   whatever it returns) — defaulting to carrying that over as-is for
   `/projects` unless you'd prefer alphabetical.
3. **`PercentComplete` display formatting.** The formula now confirmed
   (`Math.Round(..., 1)`) yields one decimal place (e.g. `66.7`) —
   recommend displaying it exactly as computed (e.g. "66.7%") rather than
   re-rounding further in the UI layer.

---

**To proceed:** Review this proposal and approve to begin planning.
