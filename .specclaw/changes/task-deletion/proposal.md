# Proposal: Task deletion (cascade)

**Created:** 2026-08-03
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

Nothing in the rebuild can remove a task yet. Every prior backlog item
adds data (objectives, tasks, status changes, checklist ticks, meetings,
notes, accountability rows) — this is the first item that removes it.
Without it, a Manager who creates a task by mistake, or wants to clean up
stale work, has no way to do so, and every downstream row it owns
(checklist items, progress notes, status history, task owners) is stuck
permanently.

Rebuild-backlog item 9 maps to Manager Planner Desktop only — Executive
Planning Desktop has no task-delete UI at all, per functional-spec.md's
Named Gaps. Reading the real legacy source directly confirms the exact
mechanics.

`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:72-79`:

```csharp
/// <summary>Deletes a task and its checklist, notes, owners and status history (cascade).</summary>
public async Task DeleteTaskAsync(int taskId)
{
    var t = await _db.WorkItems.FindAsync(taskId);
    if (t is null) return;
    _db.WorkItems.Remove(t);
    await _db.SaveChangesAsync();
}
```

The method itself does nothing clever — the "cascade" is entirely a
property of the `WorkItem`→`ProgressNote`/`StatusChange`/`ChecklistItem`
`Cascade` relationships (and `TaskOwner`'s cascade on both FKs) already
configured in `PlanningDbContext.OnModelCreating`. **This rebuild's schema
already has every one of these cascade rules**, confirmed by reading
`src/ManagerPlanner.Core/Data/PlanningDbContext.cs` directly:
`ChecklistItem`→`WorkItem` (`:104-107`), `ProgressNote`→`WorkItem`
(`:164-167`), `StatusChange`→`WorkItem` (`:187-190`), and
`TaskOwner`→`WorkItem`/`TaskOwner`→`User` (both `:127-135`) are all
`DeleteBehavior.Cascade` already — they were scaffolded in
`scaffold-blazor-solution`'s `InitialCreate` migration and have never
needed to change. **No schema or migration work is needed for this item**
— it is purely a service method plus a UI trigger.

The real legacy caller
(`../manager-planner/src/ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:233-247`)
confirms the confirmation-dialog text exactly:

```csharp
[RelayCommand]
private async Task DeleteTask()
{
    if (SelectedTask is null) { MessageRequested?.Invoke("Select a task to delete."); return; }
    var title = SelectedTask.Title;
    if (ConfirmAsync is not null &&
        !await ConfirmAsync($"Delete task '{title}' and its checklist and notes?\nThis cannot be undone."))
        return;

    await _service.DeleteTaskAsync(SelectedTask.Id);
    SelectedTask = null;
    Notes.Clear();
    SelectedTaskTitle = "(no task selected)";
    await ReloadGridAsync();
    await LoadAccountabilityAsync();
    StatusMessage = $"Task '{title}' deleted.";
}
```

Two things this confirms beyond the doc summary: the exact confirmation
text (`"Delete task '{title}' and its checklist and notes?\nThis cannot
be undone."`), and that a successful delete also reloads the
Accountability rows, not just the grid — a task disappearing changes what
that report shows.

## Proposed Solution

_What are we building? High-level approach._

1. **`PlanningService` gains one method**, ported exactly:
   - `DeleteTaskAsync(taskId)` — find by id, no-op (return, no exception)
     if not found, remove, `SaveChangesAsync()`. `IDbContextFactory` pattern
     like every other method.
2. **A "Delete" icon button on each `TaskRow`**, in a new, minimal
   "Actions" cell (this rebuild's `TaskRow` has no "select a task first"
   concept — every row already acts on its own task directly, so the
   button targets that row's task, not a page-level "selected task").
   Clicking it opens a confirmation dialog via MudBlazor's
   `IDialogService` (wired but never yet used in this rebuild —
   `.specclaw/context.md` already anticipated this: *"the intended
   mechanism once deletion — items 9/10 — actually ships a confirmation
   dialog"*), reproducing the real legacy text verbatim: `"Delete task
   '{title}' and its checklist and notes? This cannot be undone."` On
   confirm, calls `DeleteTaskAsync`, then raises a new `TaskDeleted`
   `EventCallback` wired to `ProjectDetail`'s existing full `RefreshAsync`
   — the same reuse-the-full-refresh shape already established for
   `StatusChanged`/`NoteAdded`, needed here because a deleted task must
   disappear from the objective/ungrouped lists *and* the Accountability
   rows, not just some local sub-state.
3. **No schema, migration, or new UI page** — every cascade relationship
   already exists; this item wires a button to an already-correct data
   model.

## Scope

### In Scope
- `PlanningService.DeleteTaskAsync(taskId)` — verbatim port
- A "Delete" icon button per `TaskRow`, in a new "Actions" cell
- A confirmation dialog (MudBlazor `IDialogService`) reproducing the
  legacy text exactly, with Yes/Cancel — this is the first feature in
  this rebuild to actually use the dialog infrastructure `ui-modernization`
  wired but never exercised
- A new `TaskDeleted` `EventCallback` on `TaskRow`, wired to
  `ProjectDetail.RefreshAsync`

### Out of Scope
- **Project deletion** — a separate, not-yet-built backlog item (10),
  covering `DeleteProjectAsync` and its own broader cascade.
- **A "select a task first" flow.** No legacy-equivalent selection step
  exists anywhere in this rebuild already (every prior item — status,
  checklist, notes — acts on a row directly); this item follows the same
  shape rather than reintroducing selection state.
- **A `DeleteUserAsync` method or any user-management UI.** The
  golden-master fixture `GM-033` demonstrates the `TaskOwner`→`User`
  cascade specifically (the *other* FK direction from the task-delete
  path), but that relationship is already correctly configured in the
  existing schema — verifying it needs no new service method, since this
  rebuild has no user-deletion feature to build and adding one is a
  separate, undecided scope question (domain-model.md already flags "no
  `DeleteUserAsync` exists in `PlanningService` at all").
- **Undo or soft-delete.** The legacy confirmation text itself states
  "This cannot be undone" — a hard delete, matching exactly.

## Impact

- **Files affected:** ~2 (estimated) — `PlanningService.cs` (1 new
  method), `TaskRow.razor` (Delete button + dialog + callback);
  `ProjectDetail.razor` needs only the one-line `TaskDeleted="RefreshAsync"`
  wiring on its two existing `<TaskRow>` usages
- **Complexity:** small — the service method is a two-line, already-
  cascade-correct port; the only new territory is this rebuild's first
  use of `IDialogService`, whose exact API surface will be confirmed
  against the installed `MudBlazor.dll` (9.7.0) before use, per this
  project's established practice for third-party API precision
- **Risk:** low — the cascade itself is fully golden-mastered
  (`GM-025`); the only judgment call is exact UI placement of the Delete
  action, addressed directly above rather than left open

## Open Questions

None. Both potential open questions are resolved by direct evidence: the
confirmation text is quoted verbatim from the real legacy caller, and the
`GM-033` schema-cascade question is resolved by confirming the relevant
`OnDelete(DeleteBehavior.Cascade)` configuration already exists — no new
service method needed to satisfy it.

---

**To proceed:** Review this proposal and approve to begin planning.
