# Proposal: Project deletion (cascade)

**Created:** 2026-08-03
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

Nothing in the rebuild can remove a project yet. This is the last
data-model-driven backlog item — the broadest cascade in the whole
system, since a project owns every objective, task, meeting, and
everything those in turn own. Without it, a Manager who creates a project
by mistake, or wants to retire a completed one, has no way to do so.

Rebuild-backlog item 10 maps to Manager Planner Desktop only — Executive
Planning Desktop has no delete UI at all for either projects or tasks,
per functional-spec.md's Named Gaps. Reading the real legacy source
directly confirms the exact mechanics.

`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:64-70`:

```csharp
public async Task DeleteProjectAsync(int projectId)
{
    var p = await _db.Projects.FindAsync(projectId);
    if (p is null) return;
    _db.Projects.Remove(p);
    await _db.SaveChangesAsync();
}
```

Same trivial shape as `task-deletion`'s `DeleteTaskAsync` — the entire
cascade is a property of the schema (`Project`→`Objective`/`WorkItem`/
`Meeting` Cascade, and transitively everything those own), already
confirmed present in this rebuild's schema from `scaffold-blazor-solution`.
No migration work is needed.

`../manager-planner/src/ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:218-230`
confirms the confirmation-dialog text exactly:

```csharp
[RelayCommand]
private async Task DeleteProject()
{
    if (SelectedProject is null) { MessageRequested?.Invoke("Select a project to delete."); return; }
    var name = SelectedProject.Name;
    if (ConfirmAsync is not null &&
        !await ConfirmAsync($"Delete project '{name}' and all its objectives, tasks, checklist items and notes?\nThis cannot be undone."))
        return;

    await _service.DeleteProjectAsync(SelectedProject.Id);
    await LoadProjectsAsync();
    await LoadAccountabilityAsync();
    StatusMessage = $"Project '{name}' deleted.";
}
```

## `task-deletion`'s cascade fix applies here too — confirmed, not assumed

`task-deletion` (BL-009) discovered that a byte-for-byte port of
`DeleteTaskAsync` throws `SQLite Error 19: FOREIGN KEY constraint failed`
in this rebuild whenever the task has a nested checklist item, because
`ChecklistItem.ParentId` is `Restrict` (self-reference) while this
rebuild's `IDbContextFactory` gives every call a fresh, untracked
`DbContext` — unlike the legacy desktop app's one-`DbContext`-per-session
model, which masks the issue via EF Core's own client-side cascade. That
change's own spec.md flagged explicitly: *"`BL-010`'s own
`DeleteProjectAsync` will cascade through `WorkItem` to the same
self-referencing `ChecklistItem` tree this item just discovered breaks
from a cold context."*

This proposal does not leave that as a prediction — it was verified
directly before writing this document. A naive `DeleteProjectAsync`
(plain `FindAsync` + `Remove`, matching the legacy body exactly) was
reproduced against a project containing a task with a nested (parent +
child) checklist item, using a fresh `IDbContextFactory`-style context:
it throws the identical `FOREIGN KEY constraint failed` error, one level
deeper (`Project` → `WorkItem` → `ChecklistItem`). The fix — loading
`.Include(p => p.Tasks).ThenInclude(t => t.Checklist)` before removing
the `Project` — was also verified directly against the **full**
`GM-024` shape (an objective, a task with a nested checklist, a progress
note, a status change, a task owner, and a meeting): with only that one
`Include` chain, every one of those rows cascades away correctly,
including the ones with no self-reference issue at all
(`Objective`/`ProgressNote`/`StatusChange`/`TaskOwner`/`Meeting` all
cascade fine from a cold context without being included — only the
self-referencing `ChecklistItem` tree needs to be tracked first).

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains one method**, matching the legacy's
   observable behavior but **not a byte-for-byte port** — same required
   deviation `task-deletion` already established for the analogous task
   case:
   - `DeleteProjectAsync(projectId)` — find by id (no-op if missing),
     **with `.Include(p => p.Tasks).ThenInclude(t => t.Checklist)`
     before removing** (confirmed necessary and sufficient above), then
     remove and save.
2. **A "Delete" icon button on each project row in `Projects.razor`**,
   not a page-level "select a project, then click Delete" flow —
   consistent with every prior item in this rebuild (status, checklist,
   notes, task deletion), all of which act directly on a row rather than
   reviving the legacy's selection-based interaction model. Clicking it
   opens the same `IDialogService.ShowMessageBoxAsync` confirmation
   pattern `task-deletion` established, reproducing the real legacy text
   verbatim: `"Delete project '{name}' and all its objectives, tasks,
   checklist items and notes? This cannot be undone."` On confirm, calls
   `DeleteProjectAsync` then reloads the project list — matching the
   existing `AddProjectAsync` handler's own reload-after-mutate shape
   already on this page.
3. **No schema, migration, or new UI page** — every cascade relationship
   already exists; this item wires a button to an already-correct data
   model, exactly like `task-deletion` did one level up.

## Scope

### In Scope
- `PlanningService.DeleteProjectAsync(projectId)`, including the required
  `.Include(p => p.Tasks).ThenInclude(t => t.Checklist)` fix
- A "Delete" icon button per project row on `Projects.razor`
- A confirmation dialog (MudBlazor `IDialogService`, the same pattern
  `task-deletion` established) reproducing the legacy text exactly
- Reloading the project list after a successful delete

### Out of Scope
- **A "select a project first" flow.** No legacy-equivalent selection
  step exists anywhere in this rebuild already; this item follows the
  same per-row-action shape every prior item (including `task-deletion`)
  already established.
- **Any change to `ProjectDetail.razor`.** The legacy delete capability
  is scoped to the Projects window/list only — Manager Planner Desktop
  has no delete affordance on its per-project detail views either.
- **Undo or soft-delete.** The legacy confirmation text itself states
  "This cannot be undone" — a hard delete, matching exactly.
- **Any change to `DeleteTaskAsync`, `ChangeStatusAsync`, or any other
  existing `PlanningService` method.** This item adds one new method and
  touches one existing page; nothing else in `PlanningService.cs` or any
  other Razor component is affected.

## Impact

- **Files affected:** ~2 (estimated) — `PlanningService.cs` (1 new
  method), `Projects.razor` (Delete button + dialog + reload)
- **Complexity:** small — same shape as `task-deletion`, one level up
  the entity graph; the cascade-fix requirement is already confirmed
  (not a new investigation needed during build), and the confirmation-
  dialog mechanism (`IDialogService.ShowMessageBoxAsync`) is already
  proven working in this codebase from `task-deletion`
- **Risk:** low — the cascade is fully golden-mastered (`GM-024`) and the
  fix has already been directly verified against the exact `GM-024` shape
  before this proposal was written, not left as an assumption

## Open Questions

None. The confirmation text is quoted verbatim from the real legacy
caller, and the cascade-fix requirement `task-deletion` flagged as a risk
has already been confirmed (both the failure and the fix) by direct
reproduction against the full `GM-024` scenario shape, not left as an
open risk for the build phase to discover.

---

**To proceed:** Review this proposal and approve to begin planning.
