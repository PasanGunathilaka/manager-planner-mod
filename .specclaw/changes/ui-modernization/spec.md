# Spec: UI modernization with MudBlazor

**Change:** ui-modernization
**Created:** 2026-07-28
**Status:** 🟡 Draft

## Overview

Adopts MudBlazor as the component/CSS framework for the four screens that
currently exist (`Home`, `Projects`, `ProjectDetail` + its Planner Grid,
`TaskRow`), replaces the bare two-link nav with a real `MudLayout` shell,
restyles the default `Error` page, and adds a root `README.md` — all with
**zero changes to `PlanningService`, `PlanningRules`, entities, or
migrations**. Resolves the proposal's four open questions: restyle only
what's built (nothing scaffolded ahead for items 6–8); switch the whole
app to global `InteractiveServer` rendering (MudBlazor's dialog/popover/
snackbar providers require it); keep the existing `<table>`/`TaskRow`
structure styled via `MudSimpleTable` rather than a full `MudTable`
rewrite; and write a real, if compact, README covering the whole app.

## Requirements

### Functional Requirements

1. **FR1 — MudBlazor setup.** Add the `MudBlazor` NuGet package to
   `ManagerPlanner.Web.csproj` (no version pinned in this spec — resolved
   to the latest stable at restore time). Register `builder.Services.
   AddMudServices()` in `Program.cs`. Reference MudBlazor's bundled CSS/JS
   in `App.razor` (`_content/MudBlazor/MudBlazor.min.css`,
   `_content/MudBlazor/MudBlazor.min.js`) — **no external CDN reference**
   (e.g. Google Fonts) is added; MudBlazor's own CSS font stack falls back
   to system fonts without it. Set `@rendermode="InteractiveServer"` on
   `App.razor`'s `<HeadOutlet>` and `<Routes>` so the whole app renders
   interactively by default — MudBlazor's `MudDialogProvider`/
   `MudPopoverProvider`/`MudSnackbarProvider` need an interactive render
   context to function at all.
2. **FR2 — App shell.** Replace `MainLayout.razor`'s bare `<nav>` with a
   `MudLayout` containing a `MudAppBar` (app title) and a `MudDrawer` with
   a `MudNavMenu`/`MudNavLink` for each existing route ("Home" → `/`,
   "Projects" → `/projects`), plus the root `MudThemeProvider`/
   `MudPopoverProvider`/`MudDialogProvider`/`MudSnackbarProvider`
   components (required once, app-wide).
3. **FR3 — `Home.razor` restyle.** The three DB-connectivity states
   (checking / connected / failed) render as `MudAlert`s with matching
   `Severity` (Info / Success / Error) instead of plain `<p>` text — same
   `_canConnect` conditions, no logic change.
4. **FR4 — `Error.razor` restyle.** The unhandled-error page renders via
   `MudAlert Severity="Severity.Error"` instead of `<h1 class="text-danger">`
   (a class with no effect today — no CSS framework currently defines
   `.text-danger`). The Request ID / Development-mode informational text
   is preserved, restyled with `MudText`.
5. **FR5 — `Projects.razor` restyle.** The project list becomes a
   `MudList`/`MudListItem` (each item's `Href` navigates to
   `/projects/{id}`, same as the current `<a href>`); the create-project
   form keeps its existing `<EditForm Model="this"
   OnValidSubmit="AddProjectAsync">` wiring exactly as-is, with only the
   input **controls** swapped to `MudTextField` and the submit `<button>`
   to `MudButton` — `AddProjectAsync`'s body, its
   `PlanningRules.ValidateProjectName` catch, and the inline error display
   (now a `MudAlert`) are unchanged. Loading/empty states become
   `MudAlert`s with the same messages ("Loading projects…" / "No projects
   yet.").
6. **FR6 — `ProjectDetail.razor` restyle.** Summary counts (Total/Done/
   In progress/Blocked/Not started/Overdue/% complete) become a
   `MudGrid` of `MudPaper` stat tiles, same values from the same
   `GetProjectSummaryAsync` call. The add-objective and add-task forms
   keep their existing `<EditForm>`/`OnValidSubmit` wiring, with
   `MudTextField`/`MudSelect<int?>`/`MudDatePicker<DateTime?>`/
   `MudCheckBox<bool>` replacing the current plain controls — the
   Objective and Assignee `<select>` elements' manual `@onchange`
   parsing (`OnObjectiveSelected`/`OnAssigneeSelected`) is replaced by
   `MudSelect<int?>`'s native `@bind-Value` support, still mapping "—
   Ungrouped —"/"— Unassigned —" to `null` and calling `AddTaskAsync` with
   the identical argument set `task-management` established (including
   the description pre-trim/null fidelity fix). The fixed 3-column
   header and every per-objective/Ungrouped task table is wrapped in
   `MudSimpleTable` for consistent styling, with the same `<TaskRow>` rows
   inside — no restructuring of the underlying `<table>`/`<tbody>` shape
   beyond that wrapper. Section headings and empty states
   ("No objectives yet."/"No tasks yet."/"Ungrouped") become `MudText`/
   `MudAlert` with the same text and the same visibility conditions.
7. **FR7 — `TaskRow.razor` restyle.** The status text becomes a
   color-coded `MudChip` (`NotStarted`→default, `InProgress`→info,
   `Blocked`→error, `Done`→success — a new, purely presentational
   `StatusColor` computed property alongside the existing `StatusText`).
   The four status buttons become a `MudButtonGroup` of `MudButton`s,
   each still calling `PlanningService.ChangeStatusAsync` with the exact
   same arguments and still invoking the `StatusChanged` `EventCallback`
   afterward — no change to `SetStatusAsync`'s body.
8. **FR8 — `README.md`.** A new root-level file (none exists today)
   covering: what the app is, prerequisites (.NET 8 SDK), how to run it
   (`dotnet run --project src/ManagerPlanner.Web`), the solution's two-
   project layout, and a dedicated "UI framework" section documenting the
   MudBlazor setup — package reference, `AddMudServices()` registration,
   where the provider components live (`MainLayout.razor`), and how to
   customize the theme (`MudThemeProvider`'s `Theme` parameter).

### Non-Functional Requirements

1. **NFR1 — Zero business-logic changes.** No `PlanningService` method
   signature changes, no new `PlanningService` method, no
   `PlanningRules` validation-rule or message-text change, no entity or
   migration change anywhere in the diff. This is a rendering/markup
   change only.
2. **NFR2 — Full functional re-verification.** Every acceptance criterion
   from `project-management`, `planner-grid`, `task-management`, and
   `task-status-transitions`'s `spec.md` files must still hold
   *functionally* (not just "it looks fine") after the restyle — verified
   explicitly against the running app + direct DB inspection, the same
   discipline those four changes used, not assumed from visual review
   alone.
3. **NFR3 — No external network dependency introduced.** MudBlazor's own
   bundled CSS/JS is the only new asset; no Google-Fonts-style CDN
   `<link>` is added, consistent with this rebuild's local-first
   character (a local SQLite file, no external services anywhere else).
4. **NFR4 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: Notes/Meeting/Accountability-report UI,
   task/project delete UI or confirmation dialogs, checklist-tree or
   OVERDUE/discovered-badge rendering — rebuild-backlog items 6–10 and 5
   respectively, none of which are built yet. `TaskRow`'s checklist cell
   stays the same placeholder, restyled only for visual consistency.

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors
   after the `MudBlazor` package reference is added.
2. **AC2** — Every page renders without a JavaScript console error, and
   `MudThemeProvider`/`MudPopoverProvider`/`MudDialogProvider`/
   `MudSnackbarProvider` are present exactly once (in `MainLayout.razor`),
   not duplicated per-page.
3. **AC3** — `MainLayout` renders a `MudAppBar` and a `MudDrawer` with
   working nav links to `/` and `/projects`; clicking each link navigates
   correctly.
4. **AC4** — `Home.razor` shows the correct `MudAlert` severity/text for
   each of the three `_canConnect` states (`null`/`true`/`false`).
5. **AC5** — Creating a project via the restyled form still persists
   correctly via `AddProjectAsync`; an empty or over-120-char name still
   shows `PlanningRules.ValidateProjectName`'s exact message inline (now
   via `MudAlert`) and creates no row — re-confirming
   `project-management`'s AC2/AC3 hold functionally.
6. **AC6** — Adding an objective via the restyled form still validates via
   `PlanningRules.ValidateObjectiveTitle` (same message on failure) and
   still assigns `SortOrder` append-only — re-confirming `planner-grid`'s
   AC2/AC3/AC4 hold functionally.
7. **AC7** — Adding a task via the restyled form still calls
   `AddTaskAsync` with the identical argument semantics
   `task-management` shipped: the Objective `MudSelect` maps its
   "— Ungrouped —" option to `null`, the Assignee `MudSelect` maps
   "— Unassigned —" to `null`, `Description` is still pre-trimmed/nulled
   by the page handler before the service call, and a task with no
   objective still appears in the "Ungrouped" section (shown only when
   non-empty) — re-confirming `task-management`'s AC2/AC3/AC7 hold
   functionally.
8. **AC8** — Clicking a status button still calls `ChangeStatusAsync`
   with the correct arguments: a real transition creates exactly one new
   `StatusChange` row and updates `CompletedUtc` per Business Rule 9; a
   repeated same-status click creates zero new rows (the no-op guard);
   the page's summary counts still update automatically via the same
   `StatusChanged` → `RefreshAsync` wiring — re-confirming
   `task-status-transitions`'s AC2/AC3/AC4/AC6 hold functionally.
9. **AC9** — Each task row's status renders as a color-coded `MudChip`
   distinguishing all four `WorkItemStatus` values from one another.
10. **AC10** — No `PlanningService` method's signature changed and no new
    method was added (still exactly the ten from `task-status-
    transitions`); every `PlanningRules` validation message string is
    byte-for-byte identical to before this change.
11. **AC11** — No Notes/Meeting/Accountability-report/delete-confirmation
    UI element exists anywhere in the diff; `TaskRow`'s checklist cell
    remains the same placeholder content.
12. **AC12** — `README.md` exists at the repo root and documents the app,
    prerequisites, how to run it, and a MudBlazor setup section covering
    the package reference, service registration, and theme-customization
    pointer.
13. **AC13** — No external CDN `<link>`/`<script>` reference (e.g. Google
    Fonts) appears anywhere in `App.razor`.

## Edge Cases

- **`MudSelect<int?>`'s native nullable binding replacing the manual
  `@onchange` parsing** `task-management` used for the Objective/Assignee
  dropdowns — verify the "— Ungrouped —"/"— Unassigned —" default options
  still resolve to `null` exactly as before, since this is the one
  internal binding-mechanism change beyond pure visual restyling in this
  change.
- **`Home.razor` currently has no explicit `@rendermode`** (it renders
  fine as static SSR today, since it has no interactive controls) — once
  global `InteractiveServer` rendering applies app-wide, confirm it still
  renders correctly with no behavior change (nothing on that page
  requires interactivity either way).
- **Pre-existing data from prior verification sessions** (e.g. the "ffg"
  project's `Objective A`, `Full form task`, `Ungrouped task` rows created
  during `task-management`/`task-status-transitions` verification) must
  still render correctly under the restyled UI — schema is untouched, but
  this is a good end-to-end smoke test that real, pre-existing rows
  survive a pure-presentation change.
- **MudBlazor's own required JS bundle** (`MudBlazor.min.js`, needed for
  ripple effects, popover positioning, etc.) loading alongside the
  existing `_framework/blazor.web.js` — confirm no script-load conflict
  or ordering issue.

## Dependencies

- **Depends on:** `project-management`, `planner-grid`, `task-management`,
  `task-status-transitions` — every screen this change restyles was built
  by one of those four changes.
- **Blocks:** nothing new — this doesn't gate any future backlog item,
  though items 6/7/8 (Meetings, Notes, Accountability) will build their
  eventual UI directly against the MudBlazor design system this change
  establishes, rather than needing a second restyle pass later.

## Notes

All four of the proposal's open questions are resolved by proceeding to
this spec: (1) restyle only the four currently-built screens plus `Error`
and `MainLayout` — nothing scaffolded ahead for Notes/Meetings/
Accountability; (2) the whole app switches to global `InteractiveServer`
rendering; (3) the Planner Grid keeps its existing `<table>`/`TaskRow`
structure, styled via `MudSimpleTable` rather than a full `MudTable`
rewrite; (4) `README.md` is a real, compact, whole-app document, not a
MudBlazor-only snippet.
