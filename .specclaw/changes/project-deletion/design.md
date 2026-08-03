# Design: Project deletion (cascade)

**Change:** project-deletion
**Created:** 2026-08-03

## Technical Approach

1. Add `DeleteProjectAsync(projectId)` to the existing `ManagerPlanner.Core/
   Services/PlanningService.cs` (no new file), following the established
   `IDbContextFactory<PlanningDbContext>` pattern — **not a byte-for-byte
   port**; it must `.Include(p => p.Tasks).ThenInclude(t => t.Checklist)`
   before removing (see Key Decision 1 — already confirmed necessary and
   sufficient before this design was written, not a build-time
   discovery).
2. Extend `Projects.razor`'s existing `MudList`/`MudListItem` loop with a
   `MudIconButton` (`Icons.Material.Filled.Delete`, `Color.Error`) inside
   each item's child content, using `@onclick:stopPropagation` so it
   doesn't also trigger the item's own `Href` navigation. Its click
   handler calls `IDialogService.ShowMessageBoxAsync` with the exact
   legacy confirmation text, and — only if the result is `true` — calls
   `PlanningService.DeleteProjectAsync(project.Id)` then reloads
   `_projects`.
3. No entity, schema, or migration changes — every cascade relationship
   this deletion relies on already exists.

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── ... eighteen existing methods ...
└── DeleteProjectAsync(projectId)  [new]
    └── Include(p => p.Tasks).ThenInclude(t => t.Checklist) →
        if null return → Remove → SaveChangesAsync
        (every other cascade — Objective/ProgressNote/StatusChange/
         TaskOwner/Meeting — is schema-driven; only the self-referencing
         ChecklistItem tree needs to be tracked first)

src/ManagerPlanner.Web/Components/Pages/Projects.razor   (extended)
├── existing: MudList of projects (Href-navigable MudListItem per project)
│   └── new: MudIconButton(Icons.Material.Filled.Delete), @onclick:stopPropagation
│       └── DeleteProjectAsync(project) handler
│           └── DialogService.ShowMessageBoxAsync("Delete project",
│                 "Delete project '{name}' and all its objectives, tasks,
│                  checklist items and notes?\nThis cannot be undone.",
│                 yesText: "Delete", cancelText: "Cancel")
│               → if true: PlanningService.DeleteProjectAsync(project.Id)
│                   → _projects = await PlanningService.GetProjectsAsync();
└── existing: "Add project" form (unchanged)
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `DeleteProjectAsync(projectId)` |
| `src/ManagerPlanner.Web/Components/Pages/Projects.razor` | Modify | + Delete icon button per row (confirmation dialog + reload) |

## Data Model Changes

None. Every relationship this cascade relies on
(`Objective`/`WorkItem`/`Meeting`→`Project` Cascade,
`ChecklistItem`/`ProgressNote`/`StatusChange`→`WorkItem` Cascade,
`TaskOwner` Cascade on both FKs) already exists in
`PlanningDbContext.OnModelCreating`, confirmed by reading the file
directly.

## API Changes

None. No HTTP/JSON API — the component calls `PlanningService` directly.

## Key Decisions

1. **`DeleteProjectAsync` requires `.Include(p => p.Tasks).ThenInclude(t
   => t.Checklist)` before removing — the same required deviation
   `task-deletion` established for `DeleteTaskAsync`, one level up the
   entity graph.** Confirmed by direct reproduction *before this design
   was written* (documented in proposal.md): a literal `FindAsync` +
   `Remove` throws `SQLite Error 19: FOREIGN KEY constraint failed`
   against a fresh, untracked context whenever any task under the
   project has a nested checklist item — `ChecklistItem.ParentId`'s
   self-referencing `Restrict` constraint is the root cause, exactly as
   `task-deletion`'s own finding described. The fix was also confirmed
   *sufficient* against the full `GM-024` shape: `Objective`/
   `ProgressNote`/`StatusChange`/`TaskOwner`/`Meeting` all cascade
   correctly from a cold context without being included — only the
   self-referencing subtree needs tracking. No further investigation is
   needed during build; this is a known, verified fact going in.
2. **The Delete icon button uses `@onclick:stopPropagation`, not a
   restructured navigation model.** `MudListItem<T>` (confirmed via
   reflection against the installed `MudBlazor.dll`, 9.7.0) exposes both
   `Href` (for the existing row-click navigation) and arbitrary
   `ChildContent` — placing an interactive icon button inside that
   content and stopping click propagation is the standard, minimal way to
   add a second action to an already-clickable row, without replacing
   `Href` with a manual `OnClick`/`NavigationManager.NavigateTo` scheme.
3. **Reload `_projects` after a successful delete, reusing
   `AddProjectAsync`'s existing shape** — no new refresh/callback
   mechanism is introduced. `Projects.razor` has no parent/child
   component relationship the way `TaskRow`/`ProjectDetail` do, so the
   `EventCallback`-to-`RefreshAsync` pattern those components established
   doesn't apply here; a direct re-fetch after the mutating call is the
   simplest correct shape, matching this page's own existing convention.
4. **No changes to `ProjectDetail.razor`.** The legacy delete capability
   is scoped to the Projects window/list only (confirmed by
   functional-spec.md's exact capability quote); Manager Planner Desktop
   has no delete affordance on its per-project detail view either.
5. **No "select a project" flow is introduced.** Every prior per-row
   capability in this rebuild (status, checklist, notes, task deletion)
   already acts directly on a row; the Delete button follows the same
   shape rather than reviving the legacy's selection-based interaction
   model.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — the `Project`→`Objective`/
  `WorkItem`/`Meeting` Cascade relationships and the named legacy tests
  (`Deleting_project_cascades_to_tasks_and_notes`,
  `DeleteProject_removes_everything_under_it`) this item's behavior
  mirrors.
- `.specclaw/baseline/scenarios.md` / fixture `GM-024` — the captured
  cascade assertion grounding AC2, including its explicit "task... carrying
  a checklist tree" arrangement that makes the self-referencing fix
  directly relevant to this fixture, not a hypothetical.
- `.specclaw/changes/task-deletion/` (proposal.md, spec.md, design.md) —
  the origin of the `.Include`-before-`Remove` fix pattern this item
  reuses one level up, and its own explicit forward-warning that `BL-010`
  would need the same treatment.
- **Real legacy source**, read directly:
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:64-70` — the exact `DeleteProjectAsync`
    body (grounds FR1).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    ViewModels\MainViewModel.cs:218-230` — the exact confirmation-dialog
    text and the post-delete `LoadAccountabilityAsync()` reload (grounds
    FR3/FR4).
- **Direct reproduction against this rebuild's own code** (documented in
  proposal.md, not repeated at build time): confirmed both the failure
  (naive port) and the fix (`.Include` chain) against the full `GM-024`
  shape using a fresh, `IDbContextFactory`-equivalent context.

## Risks & Mitigations

- **Risk:** a future editor could "simplify" `DeleteProjectAsync` back to
  a plain `FindAsync` + `Remove`, since that's what the legacy source
  shows and the bug only manifests with a specific data shape (a nested
  checklist under some task in the project). **Mitigation:** documented
  explicitly in spec.md FR1, this design's Key Decision 1, and a code
  comment on the method itself (matching `task-deletion`'s own
  precedent) — a future reader hits the explanation before assuming the
  `Include` is decorative.
- **Risk:** `@onclick:stopPropagation` on the Delete button could fail to
  fully suppress the row's `Href` navigation if `MudListItem`'s internal
  click handling differs from a plain anchor's. **Mitigation:** AC6
  explicitly requires live verification (click Delete → no navigation;
  click elsewhere on the row → navigates), not just a code-level
  assumption that `stopPropagation` works.
- **Risk:** forgetting the reload-after-delete step would leave a stale,
  now-nonexistent project visible in the list until a manual page
  refresh. **Mitigation:** FR4/AC4 require confirming the row disappears
  without a manual reload, exercised live during build verification.
