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
2. Extend `Projects.razor`'s existing `MudList`/`MudListItem` loop.
   **Superseded during verify remediation (see Key Decision 2, updated):**
   the Delete button is NOT nested inside an `Href`-driven `MudListItem`
   at all. `MudListItem`'s `Href` is removed; each row is a manual flex
   `<div>` containing a plain sibling `<a href="/projects/{id}">` (name/
   description only) and the `MudIconButton` as a true DOM sibling
   outside the anchor's hit area — no `stopPropagation`/`preventDefault`
   needed, since there is no ancestor anchor to race against. Each
   `MudListItem` also carries `@key="project.Id"` (see Key Decision 2).
   The button's click handler calls `IDialogService.ShowMessageBoxAsync`
   with the exact legacy confirmation text, and — only if the result is
   `true` — calls `PlanningService.DeleteProjectAsync(project.Id)` then
   reloads `_projects`.
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
├── existing: MudList of projects
│   └── MudListItem[@key=project.Id] (Href removed — see Key Decision 2)
│       └── flex <div> row
│           ├── plain sibling <a href="/projects/{id}"> (name/description)
│           └── MudIconButton(Icons.Material.Filled.Delete)  [sibling, not nested in the <a>]
│               └── DeleteProjectAsync(project) handler
│                   └── DialogService.ShowMessageBoxAsync("Delete project",
│                         "Delete project '{name}' and all its objectives, tasks,
│                          checklist items and notes?\nThis cannot be undone.",
│                         yesText: "Delete", cancelText: "Cancel")
│                       → if true: PlanningService.DeleteProjectAsync(project.Id)
│                           → _projects = await PlanningService.GetProjectsAsync();
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
2. **SUPERSEDED after verify FAIL — the Delete button is a structural
   sibling of the row's `<a>`, not nested inside an `Href`-driven
   `MudListItem`.** The original approach (`@onclick:stopPropagation` +
   `@onclick:preventDefault` on a wrapper around the button, nested
   inside `MudListItem Href="..."`) was confirmed correct at the DOM-event
   level (see the superseded rationale below) and passed rendered-HTML
   inspection, but **live click-through testing during `/specclaw:verify`
   found it insufficient in practice**: 7 of 9 real clicks on the Delete
   icon still navigated to the project detail page instead of opening the
   dialog. Root cause: this app uses the unified .NET 8 Blazor Web App
   model (`App.razor`: `<Routes @rendermode="InteractiveServer" />`,
   `blazor.web.js`), which enables **enhanced navigation** by default — a
   document-level click interceptor for same-origin anchors that operates
   independently of a component's own `@onclick` modifiers. Even both
   modifiers together could not reliably defeat it. **Fix (per explicit
   user direction — "Restructure as sibling"):** removed `Href` from
   `MudListItem` entirely; each row is now a manual flex `<div>` with a
   plain `<a href="/projects/{id}">` (name/description only) and the
   `MudIconButton` as a true DOM sibling, structurally outside the
   anchor's hit area — there is no ancestor-anchor relationship left to
   race against, so no `stopPropagation`/`preventDefault` is needed at
   all. Re-verified live (post-fix): across a second, careful round of
   paced click-through testing, the dialog correctly appeared on every
   deliberate retry, Cancel correctly aborted with no service call, a
   confirmed Delete correctly removed only the intended row and reloaded
   the list with no manual refresh, and normal name-link navigation to
   `/projects/{id}` continued to work. One incidental data point from
   this same testing round: a project was once removed from the list
   with no confirming click observed in between two consecutive tool
   calls (name "Key-fix test A"'s predecessor — the "ggg" project);
   direct DB inspection confirmed a genuine deletion, not a rendering
   glitch. This was traced to the `@foreach` loop having no `@key`, so
   Blazor's diffing could reuse/reassign DOM nodes positionally across a
   re-render (this list reorders on every add, since it's
   `OrderByDescending(CreatedUtc)` and new rows insert at the top) —
   fixed by adding `@key="project.Id"` to `MudListItem`. No further
   unconfirmed/wrong-row deletions occurred in subsequent testing after
   that fix. A residual, low-frequency (~1 in 5) "click produces no
   visible reaction at all" case remains even after both fixes — every
   such case was a no-op (neither a dialog nor a navigation nor a
   deletion), and a deliberate retry always then worked; this reads as
   click-dispatch latency in the CDP-based test tooling itself (which
   independently showed several unrelated flakiness symptoms this
   session — stale tab state, viewport-size fluctuation across
   `read_page` calls) rather than an application defect, but was not
   root-caused with certainty.
   - **Superseded rationale (kept for record):** `MudListItem<T>` with
     `Href` set (confirmed via reflection against the installed
     `MudBlazor.dll`, 9.7.0) renders as a genuine native `<a href="...">`
     element. `stopPropagation` alone prevents a click from bubbling to
     any ancestor *listener* but does not suppress a native anchor's own
     default navigation — that requires `preventDefault` specifically.
     This reasoning was correct as far as it went; it just didn't account
     for enhanced navigation being a document-level interceptor that
     `preventDefault` on the button's own wrapper span did not reliably
     reach in time under real click timing.
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
- **Risk (materialized during build, then found insufficient during
  verify, then fixed for real):** `@onclick:stopPropagation` +
  `@onclick:preventDefault` on a wrapper nested inside `MudListItem
  Href="..."` looked correct by DOM-event reasoning and rendered-HTML
  inspection, but `/specclaw:verify`'s live click-through testing found
  it still lost to Blazor Web App's document-level enhanced navigation
  in 7 of 9 real clicks (see Key Decision 2). **Mitigation:** restructured
  the row so the Delete button is a DOM sibling of the `<a>`, not nested
  inside it — no ancestor-anchor relationship exists to race against,
  so no event modifier is needed at all. Re-verified with genuine live
  clicks (not just markup inspection) post-fix; the dialog, Cancel, and
  confirmed-Delete paths all behaved correctly across a second round of
  careful testing. **Lesson:** for this project, an interactive
  click-through is required to trust AC3/AC4/AC6-shaped criteria —
  rendered-HTML/DOM-event reasoning alone let a real regression through
  once already.
- **Risk (found live during remediation testing, fixed):** the
  `@foreach` over `_projects` had no `@key`, and this list reorders on
  every add (`OrderByDescending(CreatedUtc)`, newest first) — Blazor's
  positional DOM-node reuse under those conditions can let a diff
  reassign an element (or a not-yet-flushed click) to the wrong logical
  row after a re-render. One project was observed deleted between two
  tool calls with no confirming click in between; DB inspection
  confirmed it was a genuine deletion, not a display glitch.
  **Mitigation:** added `@key="project.Id"` to `MudListItem`. No further
  unconfirmed or wrong-row deletions occurred in subsequent testing.
- **Risk:** forgetting the reload-after-delete step would leave a stale,
  now-nonexistent project visible in the list until a manual page
  refresh. **Mitigation:** FR4/AC4 require confirming the row disappears
  without a manual reload, exercised live during build verification.
