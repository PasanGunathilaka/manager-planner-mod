# Design: Task deletion (cascade)

**Change:** task-deletion
**Created:** 2026-08-03

## Technical Approach

1. Add `DeleteTaskAsync(taskId)` to the existing `ManagerPlanner.Core/
   Services/PlanningService.cs` (no new file), following the established
   `IDbContextFactory<PlanningDbContext>` pattern — **not a byte-for-byte
   port**; it must `.Include(w => w.Checklist)` before removing (see Key
   Decision 6, discovered during T1's build, not anticipated at design
   time).
2. Extend `TaskRow.razor` with a new "Actions" cell: a small
   `MudIconButton` (`Icons.Material.Filled.Delete`, `Color.Error`), whose
   click handler calls `IDialogService.ShowMessageBoxAsync` with the exact
   legacy confirmation text, and — only if the result is `true` — calls
   `PlanningService.DeleteTaskAsync(WorkItem.Id)` then raises a new
   `TaskDeleted` `EventCallback`.
3. Wire `TaskDeleted="RefreshAsync"` on both existing `<TaskRow>` usages
   in `ProjectDetail.razor` — no other change to that file.
4. No entity, schema, or migration changes — every cascade relationship
   this deletion relies on already exists.

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── ... seventeen existing methods ...
└── DeleteTaskAsync(taskId)  [new]
    └── FindAsync → if null return → Remove → SaveChangesAsync
        (cascade to ChecklistItem/ProgressNote/StatusChange/TaskOwner is
         entirely schema-driven — no application code needed for it)

src/ManagerPlanner.Web/Components/Pages/TaskRow.razor   (extended)
├── existing: title/badges cell | owner/status cell | checklist cell | notes cell
├── new: [Parameter] EventCallback TaskDeleted
├── new: @inject IDialogService DialogService
└── new: "Actions" cell
    └── MudIconButton(Icons.Material.Filled.Delete) → DeleteTaskAsync() handler
        └── DialogService.ShowMessageBoxAsync("Delete task",
              "Delete task '{title}' and its checklist and notes?\nThis cannot be undone.",
              yesText: "Delete", cancelText: "Cancel")
            → if true: PlanningService.DeleteTaskAsync(WorkItem.Id) → TaskDeleted.InvokeAsync()

src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor   (extended)
└── both <TaskRow> usages gain TaskDeleted="RefreshAsync"
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `DeleteTaskAsync(taskId)` |
| `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` | Modify | + "Actions" cell (Delete icon button + confirmation dialog), + `TaskDeleted` `EventCallback` |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | + `TaskDeleted="RefreshAsync"` on both `<TaskRow>` usages |

## Data Model Changes

None. Every relationship this cascade relies on
(`ChecklistItem`/`ProgressNote`/`StatusChange`→`WorkItem` Cascade,
`TaskOwner`→`WorkItem`/`TaskOwner`→`User` Cascade on both FKs) already
exists in `PlanningDbContext.OnModelCreating`, confirmed by reading the
file directly (lines 104-107, 127-135, 164-167, 187-190).

## API Changes

None. No HTTP/JSON API — the component calls `PlanningService` directly.

## Key Decisions

1. **`IDialogService.ShowMessageBoxAsync(string title, string message,
   string yesText = "OK", string? noText = null, string? cancelText =
   null, DialogOptions? options = null)` is the confirmed correct
   overload** — verified via a throwaway reflection console app against
   the actually-installed `MudBlazor.dll` (9.7.0), per this project's
   established practice for third-party API precision (`ui-modernization`
   set this precedent). Called with only `yesText`/`cancelText` set (no
   `noText`), producing a two-button Yes/Cancel dialog matching the
   legacy's simple confirm shape, not MudBlazor's three-button
   Yes/No/Cancel default.
2. **The Delete action lives in a new "Actions" cell on `TaskRow`**, not
   folded into an existing cell — every existing cell (title/badges,
   owner/status, checklist, notes) already has a clear single
   responsibility; a fifth, minimal cell keeps that shape rather than
   overloading one of them.
3. **`TaskDeleted` reuses `ProjectDetail`'s existing full `RefreshAsync`**,
   the same shape already established for `StatusChanged`/`NoteAdded` —
   necessary here specifically because a deleted task must vanish from
   the objective/ungrouped lists themselves (not just some row-local
   state) and from the Accountability section, both of which only
   `RefreshAsync` re-fetches.
4. **`GM-033` is verified by reading the existing schema configuration
   directly, not by adding a `DeleteUserAsync` method.** The fixture
   demonstrates the `TaskOwner`→`User` cascade — a relationship this
   rebuild's schema has had correct since `scaffold-blazor-solution` —
   and building a user-deletion feature to exercise it live would be new,
   undecided scope outside "Task deletion."
5. **No "select a task" flow is introduced.** Every prior per-task
   capability (status buttons, checklist, notes) already acts on a
   `TaskRow` directly; the Delete button follows the same shape rather
   than reviving the legacy's selection-based interaction model this
   rebuild has consistently dropped.
6. **`DeleteTaskAsync` must `.Include(w => w.Checklist)` before removing
   — a required deviation from the legacy body, not a stylistic choice.**
   Discovered by direct reproduction during T1's build: a literal
   `FindAsync` + `Remove` (the legacy's exact code) throws `SQLite Error
   19: FOREIGN KEY constraint failed` against a fresh, untracked
   `DbContext` whenever the task has a nested checklist item (parent +
   child) — confirmed against both this rebuild's code *and* the real
   legacy `PlanningService` class, run through an identical fresh-context
   harness; the legacy method only "works" in the desktop app because its
   `PlanningService` holds one `DbContext` for the whole app session, so
   checklist rows added earlier are already tracked when delete runs,
   letting EF Core's client-side cascade (which orders a self-referencing
   tree correctly) substitute for the DB's own cascade. This rebuild's
   `IDbContextFactory` gives every call a fresh context (ADR-0002) — no
   call ever benefits from that same-session tracking — so the
   `.Include` is not optional. Confirmed as the fix: with it, EF Core
   tracks the checklist subtree and deletes it in the correct order
   itself; without it, SQLite's raw cascade cannot safely resolve
   `ChecklistItem.ParentId`'s `Restrict` self-reference together with
   `WorkItemId`'s `Cascade` in one untracked delete. `ProgressNote`/
   `StatusChange`/`TaskOwner` have no self-reference and cascade correctly
   from a cold context without being included. **This same gap will apply
   to `BL-010` (Project deletion)** one level deeper (`Project` →
   `WorkItem` → `ChecklistItem`) — its future design must not assume a
   plain `Remove` is safe either.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — the `WorkItem`→`ProgressNote`/
  `StatusChange`/`ChecklistItem` Cascade and `TaskOwner`'s Cascade-on-
  both-FKs relationships, and the named legacy tests
  (`Deleting_task_cascades_to_checklist_and_owners`,
  `DeleteTask_removes_nested_checklist`) this item's behavior mirrors.
- `.specclaw/baseline/scenarios.md` / fixtures `GM-025`, `GM-033` — the
  captured cascade assertions grounding AC2/AC6.
- `.specclaw/context.md` — the `IDbContextFactory` pattern (NFR1); the
  `StatusChanged`/`NoteAdded`-reuses-full-`RefreshAsync` precedent (Key
  Decision 3); the "verify third-party API against the installed package"
  practice (Key Decision 1); the explicit note that `IDialogService` was
  "wired but didn't use it for anything yet" pending deletion actually
  shipping (Key Decision 1's premise).
- **Real legacy source**, read directly:
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:73-79` — the exact `DeleteTaskAsync` body
    (grounds FR1).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    ViewModels\MainViewModel.cs:233-247` — the exact confirmation-dialog
    text and the post-delete `LoadAccountabilityAsync()` reload (grounds
    FR3/FR4).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Core\Data\
    PlanningDbContext.cs` (this rebuild's own file, not legacy) — the
    already-correct cascade configuration (grounds AC6/Key Decision 4).

## Risks & Mitigations

- **Risk:** a future editor could assume `IDialogService.ShowMessageBoxAsync`'s
  default three-button (Yes/No/Cancel) shape is being used, and add a
  `noText` that doesn't belong. **Mitigation:** documented explicitly here
  and in spec.md FR3 that only `yesText`/`cancelText` are set, matching
  the legacy's simple two-button confirm.
- **Risk:** `GM-033`'s schema-level verification (no new code) could read
  as an incomplete implementation of "what the fixture covers."
  **Mitigation:** spec.md AC6 and this design's Key Decision 4 state
  explicitly why no new service method is needed, and name the exact file/
  lines already satisfying it.
- **Risk:** forgetting to wire `TaskDeleted="RefreshAsync"` on one of
  `ProjectDetail.razor`'s two `<TaskRow>` usages (per-objective loop vs.
  Ungrouped section) would leave deletions from one section not
  refreshing the Accountability rows. **Mitigation:** tasks.md calls out
  both usages explicitly, matching the same two-usage pattern
  `Meetings`/`NoteAdded` already had to be wired onto.
