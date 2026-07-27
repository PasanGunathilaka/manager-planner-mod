# Tasks: Objective grouping and the planner grid

**Change:** planner-grid
**Created:** 2026-07-27
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) port the two `PlanningService` methods this
item needs, (2) extend `/projects/{id}` with the Planner Grid section and
verify end-to-end. No task touches task-row rendering, the inline add-task
form, or objective edit/delete/reorder — those stay out of scope per
spec.md NFR2/AC7.

## Tasks

### Wave 1 — Core business logic

- [x] `T1` — Add `AddObjectiveAsync` + `GetPlannerForProjectAsync` to `PlanningService`
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs` (both methods already exist there, roughly lines 116-143) — port them, not the doc summary. `AddObjectiveAsync(int projectId, string title, string? keyResult = null)`: validate via `PlanningRules.ValidateObjectiveTitle(title)`, trim, `SortOrder = await db.Objectives.Where(o => o.ProjectId == projectId).CountAsync()` (append-only, count-based), save, return the `Objective`. `GetPlannerForProjectAsync(int projectId)`: `db.Objectives.Where(o => o.ProjectId == projectId).OrderBy(o => o.SortOrder).Include(o => o.Tasks).ThenInclude(t => t.Assignee).Include(o => o.Tasks).ThenInclude(t => t.Owners).ThenInclude(w => w.User).Include(o => o.Tasks).ThenInclude(t => t.Checklist).ToListAsync()` — port the full Include chain exactly even though `Tasks` will be empty for now (design.md Key Decision 1). Both methods use `IDbContextFactory<PlanningDbContext>` like the existing four methods — open/dispose a short-lived context per call, don't hold one on the service.

### Wave 2 — Page + verification

- [x] `T2` — Planner Grid section on `/projects/{id}` + end-to-end verification
  - Files: `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: Add a new section below the existing summary/Refresh block. Model the add-objective form directly on `Projects.razor`'s create-project form (same `EditForm`/`InputText`/catch-`ValidationException`-and-show-`.Message`-inline/in-place-refresh pattern) — single required Title field, **no Key Result input** (spec.md NFR3/AC8 — the legacy app never exposes one; don't add it). Always render the fixed 3-column header ("Tasks" | "Owner / status" | "Progress checklist") regardless of whether the project has any objectives. Load objectives via `GetPlannerForProjectAsync(Id)`; for each, render its Title as a heading followed by "No tasks yet." (no owner/status/checklist content, no inline add-task control — out of scope). If there are zero objectives, show "No objectives yet." beneath the header. On successful add, re-fetch via `GetPlannerForProjectAsync` and update in place — no full page reload.

    Verify manually: `dotnet build` (AC1); through the running app, submit a valid title and confirm the `Objective` row persists correctly with `KeyResult == null` and the section updates without a reload (AC2); submit an empty and an over-150-char title and confirm the inline validation message with no row created (AC3); add a second objective to the same project and confirm its `SortOrder == 1` via direct DB inspection (AC4); confirm the 3-column header renders even for a project with zero objectives (AC5); confirm each objective shows only its Title + "No tasks yet." with no owner/status/checklist markup and no add-task control anywhere (AC6); confirm the add-objective form has no Key Result field (AC8). Use `form_input` (not `computer.type`) and re-`read_page` immediately before every click if testing via claude-in-chrome — see `.specclaw/context.md`'s Key Patterns for why.

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
