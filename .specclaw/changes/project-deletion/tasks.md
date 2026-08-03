# Tasks: Project deletion (cascade)

**Change:** project-deletion
**Created:** 2026-08-03
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) add `DeleteProjectAsync` to
`PlanningService`, **with the required `.Include(p => p.Tasks).ThenInclude(t
=> t.Checklist)`** (already confirmed necessary and sufficient during
planning — this is not a build-time investigation, just implement it as
specified); (2) add a Delete icon button + confirmation dialog to
`Projects.razor`, wired to call `DeleteProjectAsync` and reload the
project list on success. No task adds a "select a project" flow, touches
`ProjectDetail.razor`, or adds undo/soft-delete — those stay out of scope
per spec.md NFR2/AC8.

## Tasks

### Wave 1 — Core business logic

- [x] `T1` — Add `DeleteProjectAsync` to `PlanningService`
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs:64-70`, not the doc summary — but do NOT port it byte-for-byte. `DeleteProjectAsync(int projectId)`: open the factory-created context, `var p = await db.Projects.Include(pr => pr.Tasks).ThenInclude(t => t.Checklist).FirstOrDefaultAsync(pr => pr.Id == projectId); if (p is null) return;` (silent no-op, no exception, matching the legacy's existence-check exactly), `db.Projects.Remove(p); await db.SaveChangesAsync();`. **The `.Include(p => p.Tasks).ThenInclude(t => t.Checklist)` is required, not optional** — spec.md FR1/design.md Key Decision 1 already confirmed by direct reproduction that a plain `FindAsync` + `Remove` throws `SQLite Error 19: FOREIGN KEY constraint failed` whenever any task under the project has a nested (parent+child) checklist item, because `ChecklistItem.ParentId`'s self-referencing `Restrict` constraint can't be resolved by SQLite's own DB-level cascade from a cold, untracked context — the same root cause `task-deletion`'s `DeleteTaskAsync` fix addressed one level up. Do not simplify this back to a plain `FindAsync` — add a doc comment on the method (matching `DeleteTaskAsync`'s own precedent in this file) explaining why, so a future edit doesn't strip it. Uses `IDbContextFactory<PlanningDbContext>` like all eighteen existing methods.

    Verify manually: `dotnet build` (AC1). Seed a project (via a scratch console app or the running app) with an objective, a task (under that objective) with a nested checklist (one parent item, one child item), one progress note, one status change, one `TaskOwner` row, and one meeting, then call `DeleteProjectAsync` directly and confirm via direct SQLite inspection that zero rows remain in `Projects`, `Objectives`, `WorkItems`, `ChecklistItems`, `ProgressNotes`, `StatusChanges`, `TaskOwners`, and `Meetings` for that project's id (AC2, matching `GM-024`). Separately, call `DeleteProjectAsync` on a project with none of the above (no objectives/tasks/meetings) and confirm it succeeds without error (AC5). Confirm `DeleteProjectAsync` called twice on the same (now-deleted) id is a silent no-op the second time, not an exception (Edge Cases). Clean up any scratch data afterward.

### Wave 2 — Component + verification

- [x] `T2` — Delete icon button, confirmation dialog, and reload wiring on `Projects.razor`
  - Files: `src/ManagerPlanner.Web/Components/Pages/Projects.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: Add `@inject IDialogService DialogService` at the top alongside the existing `@inject PlanningService PlanningService`. Inside the existing `<MudListItem T="int" Href="@($"/projects/{project.Id}")">` loop, add a `MudIconButton` (`Icon="@Icons.Material.Filled.Delete"`, `Color="Color.Error"`, `Size="Size.Small"`) with `@onclick:stopPropagation="true"` (required so clicking it does not also trigger the item's `Href` navigation — confirmed via reflection that `MudListItem<T>` supports both `Href` and arbitrary child content; design.md Key Decision 2) bound to a new `DeleteProjectAsync(Project project)` handler: `var confirmed = await DialogService.ShowMessageBoxAsync("Delete project", $"Delete project '{project.Name}' and all its objectives, tasks, checklist items and notes?\nThis cannot be undone.", yesText: "Delete", cancelText: "Cancel"); if (confirmed == true) { await PlanningService.DeleteProjectAsync(project.Id); _projects = await PlanningService.GetProjectsAsync(); }` — note `ShowMessageBoxAsync` returns `bool?`, so compare with `== true`. Reuses the exact `ShowMessageBoxAsync` overload `task-deletion` already confirmed against the installed `MudBlazor.dll` (9.7.0): `Task<bool?> ShowMessageBoxAsync(string title, string message, string yesText = "OK", string? noText = null, string? cancelText = null, DialogOptions? options = null)`. No other change to `Projects.razor` — the existing "Add project" form and its `AddProjectAsync` handler stay untouched.

    Verify manually: `dotnet build` (AC1, shared with T1). Through the running app (per `.specclaw/context.md`'s established fallback — `form_input`, `read_page` immediately before every click, default straight to JS-dispatched `element.click()`): click a project's Delete button and confirm the dialog shows the exact text `"Delete project '<real name>' and all its objectives, tasks, checklist items and notes? This cannot be undone."` (AC3); click Cancel and confirm via DB inspection that the project and everything it owns still exists, and no `DeleteProjectAsync` call occurred (AC3); confirm clicking the Delete icon does NOT navigate to that project's detail page (AC6); click Delete again, confirm the dialog's "Delete" button, and confirm the row disappears from the page without a manual refresh (AC4); separately, click on the row's text (not the icon) for a *different* project and confirm it still navigates to `/projects/{id}` normally (AC6, the other half); confirm exactly nineteen `PlanningService` methods exist (AC7); confirm no change to `ProjectDetail.razor`, no "select a project" control, and no undo/soft-delete flag exists anywhere in the diff (AC8).

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
