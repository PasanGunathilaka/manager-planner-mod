# Tasks: Task deletion (cascade)

**Change:** task-deletion
**Created:** 2026-08-03
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) add `DeleteTaskAsync` to
`PlanningService`, verbatim; (2) add a Delete icon button + confirmation
dialog to a new "Actions" cell on `TaskRow`, wired to call
`DeleteTaskAsync` and refresh `ProjectDetail` on success. No task adds
project deletion, a `DeleteUserAsync` method, undo/soft-delete, or a
"select a task" flow — those stay out of scope per spec.md NFR2/AC8.

## Tasks

### Wave 1 — Core business logic

- [ ] `T1` — Add `DeleteTaskAsync` to `PlanningService`
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs:73-79`, not the doc summary. `DeleteTaskAsync(int taskId)`: open the factory-created context, `var t = await db.WorkItems.FindAsync(taskId); if (t is null) return;` (silent no-op, no exception, matching the legacy exactly), `db.WorkItems.Remove(t); await db.SaveChangesAsync();` — no manual cleanup of checklist/notes/status-history/owners; the cascade is entirely schema-driven. Uses `IDbContextFactory<PlanningDbContext>` like all seventeen existing methods. As part of this task, also confirm (read-only, no code change) that `src/ManagerPlanner.Core/Data/PlanningDbContext.cs` already configures `TaskOwner`→`User` as `DeleteBehavior.Cascade` (around lines 132-135) — this is spec.md AC6, satisfied by existing schema, not new code in this task.

    Verify manually: `dotnet build` (AC1). Seed a task (via a scratch console app or the running app) with a nested checklist (one parent item, one child item), one progress note, at least one status change (e.g. transition it to `InProgress` then back), and one `TaskOwner` row, then call `DeleteTaskAsync` directly and confirm via direct SQLite inspection that zero rows remain in `WorkItems`, `ChecklistItems`, `ProgressNotes`, and `TaskOwners` for that task's id (AC2, matching `GM-025`). Separately, call `DeleteTaskAsync` on a task with none of the above (no checklist/notes/status-history/owners) and confirm it succeeds without error (AC5). Confirm `DeleteTaskAsync` called twice on the same (now-deleted) id is a silent no-op the second time, not an exception (Edge Cases). Clean up any scratch data afterward.

### Wave 2 — Component + verification

- [ ] `T2` — Delete icon button, confirmation dialog, and refresh wiring on `TaskRow`/`ProjectDetail`
  - Files: `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor`, `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: In `TaskRow.razor`, add `@inject IDialogService DialogService` at the top alongside the existing `@inject PlanningService PlanningService`, and add `[Parameter] public EventCallback TaskDeleted { get; set; }` alongside the existing `StatusChanged`/`Meetings`/`NoteAdded` parameters. Add a new, minimal `<td>` ("Actions" — add a matching `<th>Actions</th>` on `ProjectDetail.razor`'s Planner Grid `<thead>` too) containing a single `MudIconButton` (`Icon="@Icons.Material.Filled.Delete"`, `Color="Color.Error"`, `Size="Size.Small"`) bound to a new `DeleteTaskAsync()` handler: `var confirmed = await DialogService.ShowMessageBoxAsync("Delete task", $"Delete task '{WorkItem.Title}' and its checklist and notes?\nThis cannot be undone.", yesText: "Delete", cancelText: "Cancel"); if (confirmed == true) { await PlanningService.DeleteTaskAsync(WorkItem.Id); await TaskDeleted.InvokeAsync(); }` — note `ShowMessageBoxAsync` returns `bool?`, so compare with `== true` (a `null`/Cancel result must not delete). Verify the exact `ShowMessageBoxAsync` overload signature against the installed `MudBlazor.dll` (9.7.0) before writing this — design.md Key Decision 1 already confirmed it via reflection (`Task<bool?> ShowMessageBoxAsync(string title, string message, string yesText = "OK", string? noText = null, string? cancelText = null, DialogOptions? options = null)`), but re-check if anything doesn't compile. In `ProjectDetail.razor`, add `TaskDeleted="RefreshAsync"` to both existing `<TaskRow WorkItem="task" StatusChanged="RefreshAsync" Meetings="_meetings" NoteAdded="RefreshAsync" />` usages (the per-objective loop and the Ungrouped section) — no other change to `ProjectDetail.razor` beyond this and the one new `<th>Actions</th>` column header (added to the Planner Grid's `<thead>` alongside the existing Tasks/Owner-status/Progress-checklist headers, to keep the table structurally valid against `TaskRow`'s new fifth `<td>`).

    Verify manually: `dotnet build` (AC1, shared with T1). Through the running app (per `.specclaw/context.md`'s established fallback — `form_input`, `read_page` immediately before every click, default straight to JS-dispatched `element.click()`): click a task's Delete button and confirm the dialog shows the exact text `"Delete task '<real title>' and its checklist and notes? This cannot be undone."` (AC3); click Cancel and confirm via DB inspection that the task and every row it owns still exist, and no `DeleteTaskAsync` call occurred (AC3); click Delete again and confirm the dialog's "Delete" button, then confirm the row disappears from the page without a manual refresh and the Accountability section no longer lists that task (AC4); confirm exactly eighteen `PlanningService` methods exist (AC7); confirm no project-deletion control, `DeleteUserAsync` method, or undo/soft-delete flag exists anywhere in the diff (AC8).

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
