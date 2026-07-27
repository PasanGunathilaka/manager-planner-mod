# Tasks: Project management — create, browse, switch, and summarize projects

**Change:** project-management
**Created:** 2026-07-27
**Total Tasks:** 5

## Summary

Five tasks across four waves: (1) port the Project-related `PlanningService`
slice + `ProjectSummary` read-model, (2) wire it into DI plus a startup
Manager bootstrap, (3) build the two Blazor pages (in parallel — they touch
different files and both only depend on the wiring), (4) wire the nav link
and verify end-to-end. No task touches Objective/WorkItem/Meeting/Note/
Accountability logic or project deletion — those are later backlog items
(spec.md NFR3/AC8).

## Tasks

### Wave 1 — Core business logic

- [x] `T1` — Implement `PlanningService` (Projects) + `ProjectSummary` read-model
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`, `src/ManagerPlanner.Core/Services/Reports.cs`
  - Estimate: medium
  - Depends: none
  - Notes: Ground-truth against the real legacy source at `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs` and `Reports.cs` — not just `domain-model.md`'s summary. Port `GetProjectsAsync` (`OrderByDescending(p => p.CreatedUtc)`, `Include(p => p.Owner)`), `AddProjectAsync(name, description, ownerId)` (validates via `PlanningRules.ValidateProjectName`, trims both strings, does NOT set `CreatedUtc`/`Status` — those come from the entity's own defaults), and `GetProjectSummaryAsync(projectId)` → `ProjectSummary` with `PercentComplete = TotalTasks == 0 ? 0 : Math.Round(100.0 * Done / TotalTasks, 1)` exactly as legacy `Reports.cs`. Constructor takes `IDbContextFactory<PlanningDbContext>` (NOT a direct `PlanningDbContext` — that's the one deliberate deviation from the legacy constructor signature, per design.md Key Decision 1); each method opens/disposes its own short-lived context via `CreateDbContextAsync()`. Add `GetCurrentManagerIdAsync()` (new, no legacy equivalent — queries `Users.Where(u => u.Role == UserRole.Manager).Select(u => u.Id).FirstAsync()`).

### Wave 2 — Wiring

- [x] `T2` — Register `PlanningService` + startup Manager bootstrap
  - Files: `src/ManagerPlanner.Web/Program.cs`
  - Estimate: small
  - Depends: `T1`
  - Notes: `builder.Services.AddScoped<PlanningService>();`. In the existing startup scope block (alongside the migration call), after migrating, check `db.Users.Any(u => u.Role == UserRole.Manager)`; if false, create exactly one `User` (fixed name/email, e.g. "Manager" / a placeholder address) and save. Must be idempotent — a second startup against an already-bootstrapped DB creates no second Manager row (spec.md AC2).

### Wave 3 — Pages

- [x] `T3` — `/projects` page: browse + create
  - Files: `src/ManagerPlanner.Web/Components/Pages/Projects.razor`, `src/ManagerPlanner.Web/Components/_Imports.razor`
  - Estimate: medium
  - Depends: `T2`
  - Notes: `@rendermode InteractiveServer`. List via `GetProjectsAsync` (Name + Description columns/rows), each linking to `/projects/{id}`. Create form: Name (required) + Description (optional) fields + "Add project" button → `GetCurrentManagerIdAsync()` then `AddProjectAsync(...)`; on `ValidationException`, show `.Message` inline, don't crash; on success, refresh the list in-place. Add `@using ManagerPlanner.Core.Domain` and `@using ManagerPlanner.Core.Services` to `_Imports.razor` (shared by T4 too).

- [x] `T4` — `/projects/{id:int}` page: summary + refresh
  - Files: `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`
  - Estimate: small
  - Depends: `T2`
  - Notes: `@rendermode InteractiveServer`. Call `GetProjectSummaryAsync(id)` on load and again on an explicit "Refresh" button click (no auto-polling). Display Total/Done/In progress/Blocked/Not started/Overdue/% complete. If the result has `TotalTasks == 0` and empty `ProjectName`, render "Project not found" instead of the counts (design.md Key Decision 3).

### Wave 4 — Integration + verification

- [x] `T5` — Nav link + end-to-end verification
  - Files: `src/ManagerPlanner.Web/Components/Layout/MainLayout.razor`
  - Estimate: small
  - Depends: `T3`, `T4`
  - Notes: add a nav link to `/projects` in the currently-bare `MainLayout.razor`. Then verify manually: `dotnet build` (AC1); run against a fresh/empty DB and confirm exactly one Manager `User` row is created, then run again and confirm no second one is created (AC2); create a project through the UI and confirm the row/list update (AC3); submit an empty and an over-120-char name and confirm the inline validation message with no row created (AC4); insert a `WorkItem` row directly (EF Core or a scratch script) against a project with a known status mix, visit `/projects/{id}`, and confirm the counts and `PercentComplete` match the formula (AC5); update that `WorkItem` directly and confirm clicking "Refresh" (not just reloading the page) picks up the change (AC7); visit a nonexistent `/projects/999999` and confirm "Project not found" (AC6).

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
