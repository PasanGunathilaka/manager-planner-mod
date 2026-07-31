# Design: Nested checklist items and grid status badges

**Change:** nested-checklist-items-and-grid-status-badges
**Created:** 2026-07-31

## Technical Approach

1. Add one method to the existing `ManagerPlanner.Core/Services/
   PlanningService.cs` (no new file): `ToggleChecklistItemAsync`, ported
   verbatim from the real legacy source, following the established
   `IDbContextFactory<PlanningDbContext>` pattern.
2. Add a new recursive component, `ChecklistTree.razor`, alongside
   `TaskRow.razor` in `Components/Pages/` (matching the existing flat
   layout — no new `Shared/` folder introduced for one component).
   Takes a `List<ChecklistItem>` of the items at one tree level, renders
   each as a `MudCheckBox<bool>` (label + optional `"— {FullName}"`
   assignee text), and recurses into `<ChecklistTree>` again for any
   children, indented one step further.
3. Extend `TaskRow.razor`:
   - Replace the third `<td>&mdash;</td>` with a conditional: if
     `WorkItem.Checklist.Any(c => c.ParentId == null)`, render
     `<ChecklistTree Items="RootChecklistItems" />`; otherwise keep the
     existing `&mdash;`.
   - Add two computed properties, `IsOverdue`/`IsDiscovered`, next to the
     file's existing `StatusText`/`StatusColor` computed properties, and
     two conditionally-rendered `MudText`/`MudChip` badges in the
     title/deadline cell.

No entity, schema, or migration changes — `ChecklistItem` and its
cascade/`Restrict` rules already exist (`InitialCreate` migration); the
modern `PlanningDbContext`'s cascade configuration for this entity was
independently confirmed at 100% parity against the legacy golden master
in the most recent `/specclaw:verify-parity` run (all 12 `PlanningDbContext`
cases passed).

## Architecture

```
src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── ... ten existing methods ...
└── ToggleChecklistItemAsync(itemId, isDone)  [new]

src/ManagerPlanner.Web/Components/Pages/
├── TaskRow.razor                               (extended)
│   ├── existing: title+deadline | assignee+status+buttons | checklist placeholder
│   ├── new: IsOverdue / IsDiscovered computed properties
│   ├── new: OVERDUE / ⚑ discovered badges in the title/deadline cell
│   └── new: third cell renders <ChecklistTree Items="RootChecklistItems" />
│            when non-empty, else keeps the existing "—"
└── ChecklistTree.razor                         [new]
    ├── [Parameter] List<ChecklistItem> Items
    ├── @inject PlanningService
    ├── renders one MudCheckBox<bool> per item (label + optional assignee text)
    ├── recurses into <ChecklistTree Items="item.Children...OrderBy(SortOrder)" />
    │   for any item with children, indented
    └── OnToggle(item, isDone) → PlanningService.ToggleChecklistItemAsync
                                → item.IsDone = isDone (local, no page refresh)
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `ToggleChecklistItemAsync(itemId, isDone)` |
| `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` | Modify | + `IsOverdue`/`IsDiscovered` properties, + two badges, + checklist column renders `ChecklistTree` |
| `src/ManagerPlanner.Web/Components/Pages/ChecklistTree.razor` | Create | Recursive nested-checklist renderer + toggle wiring |

## Data Model Changes

None. `ChecklistItem` (fields, FKs, the `Parent` self-reference `Restrict`
rule) already exists from `scaffold-blazor-solution`'s `InitialCreate`
migration.

## API Changes

None. No HTTP/JSON API — `ChecklistTree` calls `PlanningService` directly,
as established in every prior change.

## Key Decisions

1. **Arbitrary-depth recursion, not a single fixed level.** The legacy
   `BuildTree` (`RowViewModels.cs:72-84`) recurses through
   `vm.Children.Add(Make(child))` with no depth limit — `ChecklistTree`
   ports that exact shape by having the component render itself for any
   item's children, rather than hand-rolling two hard-coded nesting
   levels.
2. **No new `.ThenInclude(c => c.Assignee)` added to either grid query.**
   Reading the real legacy `GetPlannerForProjectAsync` directly confirms
   it never eager-loads the checklist's own `Assignee` either — the
   *only* reason `ChecklistItemVm.AssigneeText` ever shows a name is EF
   Core's automatic relationship fixup, which resolves a `ChecklistItem.
   Assignee` reference "for free" only when that same `User` happens to
   already be tracked in the context (because they're *also* a task
   `Assignee` loaded by the sibling `.Include(t => t.Assignee)` in the
   same query). Adding an explicit `.ThenInclude` here would silently
   improve on a legacy behavior nobody asked to change — the rebuild
   reproduces the incidental gap exactly, matching this project's
   established "don't silently fix legacy quirks" constraint (ADR-0005).
3. **Local-only state update on toggle — no `EventCallback` bubbling to
   `ProjectDetail.RefreshAsync`.** Unlike `ChangeStatusAsync` (whose
   `StatusChanged` callback deliberately bubbles up because a status
   change can move a task in/out of the `Overdue`/`Done`/etc. counts
   `ProjectSummary` displays — `task-status-transitions` Key Decision
   4), nothing in `ProjectSummary` reads checklist state. A local
   `item.IsDone = isDone` mutation after a successful service call is
   sufficient and avoids an unnecessary full-grid re-fetch.
4. **MudBlazor semantic colors, not literal legacy hex.** `OVERDUE` uses
   `Color.Error`, `⚑ discovered` uses `Color.Warning` — resolves the
   proposal's Open Question 2 in favor of consistency with `TaskRow`'s
   existing `StatusColor` (already semantic, not hex) and
   `ui-modernization`'s system-wide move off the legacy's literal
   colors. The badge *text* ("OVERDUE", "⚑ discovered") is the actual
   legacy-fidelity requirement per functional-spec.md; the exact RGB
   values were an artifact of the discarded Win95-style skin.
5. **Checklist-item creation deferred**, resolving Open Question 1 —
   matches legacy fidelity exactly (Named Gap #5: no legacy UI ever
   creates one). This item's toggle/render logic is verified against a
   test fixture or direct DB insert, not an in-app "add checklist item"
   flow, since none exists to build against yet.
6. **`IsOverdue`/`IsDiscovered` computed client-side in `TaskRow`'s
   `@code` block**, never persisted or returned by `PlanningService` —
   matches both the legacy `RowViewModels.cs` pattern (ViewModel-
   computed, never round-tripped) and this file's own existing
   `StatusColor`/`StatusText` convention.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — `ChecklistItem` entity fields,
  DR-011 ("Toggling a checklist item stamps/clears its completion
  time"), the `Parent` self-reference `Restrict` rule.
- **Real legacy source**, read directly:
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:161-168` — the exact
    `ToggleChecklistItemAsync` body (grounds FR1).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    ViewModels\RowViewModels.cs:11-34,36-85` — `ChecklistItemVm`
    (assignee text format), `TaskRowVm`'s `IsOverdue`/`IsDiscovered`
    computed properties, and `BuildTree`'s real recursive shape (grounds
    FR2-FR6, Key Decisions 1 and 6).
  - `C:\Learnings\Projects\manager-planner\src\ManagerPlanner.Desktop\
    Views\PlannerGridView.axaml:60-105` — the exact checklist
    `TreeView`/`CheckBox` markup and the badges' exact text/hex colors
    (grounds FR2-FR6, Key Decision 4).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:97-108` (`GetPlannerForProjectAsync`) —
    confirms no `.ThenInclude` for the checklist's `Assignee` exists in
    the legacy query either (grounds Key Decision 2).
- `.specclaw/adr/0005-fidelity-verification-strategy.md` — "Item 5 —
  single-subtree checklist delete (a path the legacy app never runs; no
  golden master exists — a human must define intended behaviour)"
  (grounds the deletion exclusion in spec.md's Notes).
- `.specclaw/context.md` — the `IDbContextFactory` pattern (NFR1); the
  established `TaskRow`/`StatusChanged`-bubbles-to-`RefreshAsync` shape
  this item deliberately does *not* reuse (Key Decision 3, contrasted
  directly); MudBlazor 9.7.0 as the component framework and the "verify
  a third-party API surface before writing code" convention (applies to
  `MudCheckBox<bool>`'s exact `Value`/`ValueChanged` members, to be
  confirmed during build per Risks below).
- Most recent `/specclaw:verify-parity` run — confirmed the modern
  `PlanningDbContext`'s cascade/`Restrict` configuration (including
  `ChecklistItem.Parent`) already matches the legacy golden master at
  100%, grounding "no schema changes needed" above.

## Risks & Mitigations

- **Risk:** a recursive Blazor component could in principle infinite-
  loop against cyclic `ParentId` data. **Mitigation:** no create-UI
  exists in this item at all (NFR2/Key Decision 5), so no new write path
  can introduce such a cycle; this exact recursive shape is a direct
  port of `BuildTree`, which the legacy app already runs safely against
  the same schema and seed data today.
- **Risk:** the incidental EF Core relationship-fixup quirk (an
  assignee's name showing only sometimes, despite `AssigneeId` always
  being set) could be mistaken for a bug during verification.
  **Mitigation:** documented explicitly in spec.md's Edge Cases and
  AC4 — verify against the exact query behavior a fresh read of the
  legacy source predicts, not "every assigned item shows a name."
- **Risk:** `MudCheckBox<bool>`'s exact API (`Value`/`ValueChanged`
  parameter names, `Dense` availability) is unconfirmed against the
  installed MudBlazor 9.7.0 package. **Mitigation:** per context.md's
  established convention, confirm the exact API via a throwaway
  reflection probe (or a quick doc/IntelliSense check) against the
  installed `MudBlazor.dll` before finalizing `ChecklistTree.razor`'s
  markup — captured as an explicit build-time step in tasks.md.
- **Risk:** deferring checklist-item creation (Key Decision 5) means
  this feature has nothing to render in a running app until item 11
  ships. **Mitigation:** explicit, proposal-approved scoping decision,
  not an oversight — verified via test fixture/direct DB insert per
  tasks.md's verification notes, same as spec.md's Notes section
  states.
