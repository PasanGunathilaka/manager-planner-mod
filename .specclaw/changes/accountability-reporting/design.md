# Design: Accountability reporting (promised-vs-delivered verdicts)

**Change:** accountability-reporting
**Created:** 2026-08-03

## Technical Approach

1. Add `AccountabilityRow` to the existing `ManagerPlanner.Core/Services/
   Reports.cs` (no new file) — a mutable class alongside the existing
   `ProjectSummary`, matching this project's established DTO style.
2. Add two methods to `ManagerPlanner.Core/Services/PlanningService.cs`:
   `GetAccountabilityReportAsync(projectId)` and
   `GetAccountabilityForAllProjectsAsync()`, both ported verbatim from the
   real legacy source, following the established
   `IDbContextFactory<PlanningDbContext>` pattern.
3. Extend `ProjectDetail.razor` with a new "Accountability" section (a
   read-only `MudSimpleTable`, following the same pattern as the Meetings
   and Notes sections) and load `GetAccountabilityReportAsync` in both
   `OnInitializedAsync` and `RefreshAsync`.
4. Add a new page, `Accountability.razor` (`@page "/accountability"`) —
   this rebuild's first genuinely new top-level route — rendering
   `GetAccountabilityForAllProjectsAsync`'s rows in the same table shape
   plus a `Project` column. Add a matching `MudNavLink` to
   `MainLayout.razor`.
5. `TaskRow.razor`'s `AddNoteAsync` gains a new `[Parameter] public
   EventCallback NoteAdded { get; set; }`, invoked on success alongside
   the existing local `_notes` reload. `ProjectDetail.razor` wires
   `NoteAdded="RefreshAsync"` on both existing `<TaskRow>` usages — the
   same full-page-refresh reuse already established for `StatusChanged`.

No entity, schema, or migration changes — `AccountabilityRow` is a
computed, non-EF-mapped DTO exactly like the legacy `Reports.cs`; every
field it reads (`WorkItem`, `ProgressNote`) already exists.

## Architecture

```
src/ManagerPlanner.Core/Services/Reports.cs   (extended)
├── ProjectSummary   (existing)
└── AccountabilityRow   [new]
    ├── WorkItemId, TaskTitle, ProjectName, AssigneeName, Status, Deadline
    ├── LatestPromisedDate, LatestPromiseText, LatestPromiseRecordedUtc, CompletedUtc
    ├── IsOverdue, PromiseBroken, PromiseKept   (settable)
    └── Verdict { get; }   — PromiseKept → PromiseBroken → IsOverdue → LatestPromisedDate.HasValue → else

src/ManagerPlanner.Core/Services/PlanningService.cs   (extended)
├── ... fifteen existing methods ...
├── GetAccountabilityReportAsync(projectId)  [new]
│   └── Include(Assignee, Project, Notes) → per-task latest-promise selection →
│       IsOverdue/PromiseKept/PromiseBroken → 3-key sort
└── GetAccountabilityForAllProjectsAsync()  [new]
    └── per-project GetAccountabilityReportAsync → concat → 4-key sort (+ProjectName)

src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor   (extended)
├── existing: summary | Planner Grid | Meetings
├── new: "Accountability" heading
├── new: _accountabilityRows, loaded in OnInitializedAsync + RefreshAsync
├── new: read-only MudSimpleTable (Task | Assignee | Status | Deadline | Promised | Verdict)
│   └── Verdict rendered via a VerdictColor(string) → Color helper (Success/Error/Warning/Info/Default)
└── both <TaskRow> usages gain NoteAdded="RefreshAsync"

src/ManagerPlanner.Web/Components/Pages/Accountability.razor   [new]
├── @page "/accountability"
├── _rows, loaded in OnInitializedAsync via GetAccountabilityForAllProjectsAsync
└── read-only MudSimpleTable (Project | Task | Assignee | Status | Deadline | Promised | Verdict)

src/ManagerPlanner.Web/Components/Layout/MainLayout.razor   (extended)
└── + <MudNavLink Href="/accountability">Accountability</MudNavLink>

src/ManagerPlanner.Web/Components/Pages/TaskRow.razor   (extended)
└── AddNoteAsync(): + await NoteAdded.InvokeAsync(); after the existing local _notes reload
```

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/Reports.cs` | Modify | + `AccountabilityRow` class |
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Modify | + `GetAccountabilityReportAsync(projectId)`, + `GetAccountabilityForAllProjectsAsync()` |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | + Accountability section, + `NoteAdded="RefreshAsync"` on both `<TaskRow>` usages |
| `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` | Modify | + `NoteAdded` `EventCallback`, invoked in `AddNoteAsync` |
| `src/ManagerPlanner.Web/Components/Pages/Accountability.razor` | Create | New all-projects page |
| `src/ManagerPlanner.Web/Components/Layout/MainLayout.razor` | Modify | + nav link to `/accountability` |

## Data Model Changes

None. `AccountabilityRow` is computed on-the-fly from existing
`WorkItem`/`ProgressNote` rows, exactly like the legacy `Reports.cs` — "not
EF-mapped entities... computed on-the-fly... and thrown away after
rendering" (domain-model.md).

## API Changes

None. No HTTP/JSON API — both new pages call `PlanningService` directly.

## Key Decisions

1. **The `IsOverdue`-before-promise precedence quirk is preserved
   exactly**, per the proposal's Open Question 1 recommendation and the
   captured fixture `GM-010`'s own stated intent. `AccountabilityRow.Verdict`'s
   `if` chain is a direct, unmodified port of the legacy order.
2. **`/accountability` is a genuinely new top-level route** — the first
   in this rebuild. Every prior capability extended an existing page
   because it had a natural single-project home; the all-projects scope
   has none. `.specclaw/context.md` had already anticipated exactly this:
   "Future backlog items (Accountability reporting, BL-008 — still not
   built) can add another `MudNavLink` here if they warrant a dedicated
   route."
3. **`TaskRow`'s `NoteAdded` callback reuses `ProjectDetail`'s existing
   full `RefreshAsync`**, not a new lighter method — the same
   already-established shape as `StatusChanged`, and for the same reason:
   aggregate state elsewhere on the page (now including Accountability
   rows, not just the summary counts) depends on it.
4. **No project filter and no refresh button on `/accountability`** — the
   legacy window "is read-only; no commands," and this project's
   Blazor Server navigation already re-fetches on page load without one.
5. **Verdict color-coding uses MudBlazor's semantic `Color` enum**
   (`Success`/`Error`/`Warning`/`Info`/`Default`), not custom CSS — the
   same pattern `TaskRow.StatusColor` already established, mapped
   1:1 onto the legacy's own green/red/amber/blue/grey `VerdictBrush`
   scheme.
6. **"Assignee" is used consistently on both surfaces**, not "Owner" for
   the all-projects page — both read the identical
   `AccountabilityRow.AssigneeName` field; introducing a second label for
   the same data would be arbitrary, not a fidelity requirement.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — DR-007's full precedence
  flowchart and the "not EF-mapped... computed on-the-fly" note on
  `AccountabilityRow`/`ProjectSummary`.
- `.specclaw/analysis/functional-spec.md` — the exact column sets for
  both legacy surfaces (Task/Assignee/Status/Deadline/Promised/Verdict;
  Project/Task/Owner/Deadline/Status/Promised/Verdict), the
  `VerdictBrush` color scheme, and the "the command also immediately
  recomputes the Accountability tab's rows" note grounding FR7/Key
  Decision 3.
- `.specclaw/adr/0005-fidelity-verification-strategy.md` — names the
  "Overdue (no promise)" precedence quirk as the item requiring "a
  golden-master capture and an explicit product decision before
  deviating," grounding Key Decision 1.
- `.specclaw/baseline/scenarios.md` and fixtures `GM-008` through
  `GM-018` — the full captured truth table for every `Verdict` precedence
  branch, both boundary conditions, the latest-promise-supersedes rule,
  and the genuine sort-tie order, grounding AC2–AC12.
- **Real legacy source**, read directly:
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\Reports.cs:1-49` — the exact `AccountabilityRow` fields and
    `Verdict` precedence (grounds FR1/FR4).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\
    Services\PlanningService.cs:269-346` — the exact
    `GetAccountabilityReportAsync`/`GetAccountabilityForAllProjectsAsync`
    bodies, including the extra `ProjectName` tie-break key in the
    all-projects sort (grounds FR2/FR3).
  - `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Desktop\
    ViewModels\MainWindowViewModel.cs:193-197` — the immediate
    re-query-on-note-add behavior (grounds FR7).
- `.specclaw/context.md` — the `IDbContextFactory` pattern (NFR1); the
  `StatusChanged`-reuses-full-`RefreshAsync` precedent this item extends
  to `NoteAdded` (NFR4/Key Decision 3); the already-anticipated new-route
  note (Key Decision 2); the `MudSimpleTable`-not-`MudTable` and
  semantic-`Color` conventions (Key Decision 5); the "don't silently fix
  the accountability verdict precedence" constraint (Key Decision 1).

## Risks & Mitigations

- **Risk:** preserving the `IsOverdue`-before-promise quirk could read as
  a bug during a future review, since it genuinely does mislabel a task
  with an active promise. **Mitigation:** documented explicitly in
  spec.md FR4/AC4, this design's Key Decision 1, and cross-referenced to
  `GM-010`'s own stated purpose — a future reader hits the explanation
  before assuming it's an oversight.
- **Risk:** the genuine sort-tie case (AC12) has no documented tie-break;
  a future refactor could "helpfully" add one, silently changing observed
  order. **Mitigation:** spec.md's Edge Cases states this plainly, with
  `GM-018` as the concrete reference fixture to re-check against.
- **Risk:** `TaskRow`'s new `NoteAdded` callback adds a second
  `EventCallback` parameter to an already-multi-purpose component.
  **Mitigation:** it mirrors the existing `StatusChanged` shape exactly
  (same reuse-the-full-refresh pattern), so no new component
  architecture is introduced — just one more instance of an established
  one.
- **Risk:** `/accountability` being a new top-level route means it's the
  first page in this rebuild not reachable from `ProjectDetail.razor`'s
  own navigation — a user could miss it. **Mitigation:** the new
  `MudNavLink` sits in the same persistent `MudDrawer` nav menu as
  `Home`/`Projects`, so it's always visible, not buried behind another
  page.
