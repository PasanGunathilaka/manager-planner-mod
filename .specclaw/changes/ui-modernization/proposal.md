# Proposal: UI modernization with MudBlazor

**Created:** 2026-07-28
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

The rebuild's UI is currently unstyled: reading the real files directly
confirms `MainLayout.razor` is a bare two-link `<nav>` with no chrome at
all, `app.css` is the untouched Blazor Web App template stub (validation
outline colors + a hidden error boundary, nothing else), and **no CSS or
component framework is referenced anywhere** — not even the Bootstrap the
default template usually ships with (grep across the whole `.Web` project
for `MudBlazor`/`Bootstrap`/`Blazorise` returns nothing). Forms use plain
`<input>`/`<select>` elements, status text is a bare colored `<div>`
(`TaskRow.razor`'s inline `style="color:#404080"`), and every screen —
`Home`, `Projects`, `ProjectDetail`'s Planner Grid — has zero layout
structure beyond default HTML flow. This is a real, verified gap, not an
aesthetic preference.

**Important scope-grounding, read before approving:** your request names
several surfaces and safeguards — "notes and promises," "the accountability
report," "delete behaviour," "confirmation dialogs for destructive
actions" — that **do not exist in this rebuild yet**. Only rebuild-backlog
items 1–4 are built so far (Project management, Objective grouping, Task
creation/viewing, Task status transitions — `project-management`,
`planner-grid`, `task-management`, `task-status-transitions`). Progress
notes (item 7), meetings (item 6), the accountability report (item 8), and
task/project deletion (items 9/10) haven't been built, so there is nothing
there to restyle or add confirmation dialogs to yet. Your request also
says "all existing tests must continue to pass" — **this solution has no
test project at all** (`ManagerPlanner.sln` contains only
`ManagerPlanner.Core` and `ManagerPlanner.Web`; `test_command` is unset in
`config.yaml`). Neither of these is a reason to decline the request — it's
grounding for what "preserve everything" concretely means *right now*, and
is addressed in Scope and Open Questions below rather than silently
assumed either way.

## Proposed Solution

_What are we building? High-level approach._

1. **Adopt MudBlazor** as the component/CSS framework — a single NuGet
   package (`MudBlazor`), no npm/JS build step, and first-class Blazor
   Server support on .NET 8 (this project's exact stack per
   `.specclaw/context.md`'s Technology Decisions). Register its services
   in `Program.cs`, add its CSS/JS references to `App.razor`'s `<head>`,
   and add the root `MudThemeProvider`/`MudPopoverProvider`/
   `MudDialogProvider`/`MudSnackbarProvider` components (needed for
   dialogs, snackbars, and popovers to work at all).
2. **Restyle the app shell.** Replace `MainLayout.razor`'s bare `<nav>`
   with a `MudLayout` (`MudAppBar` + `MudDrawer` with `MudNavMenu`/
   `MudNavLink` entries) — a real, persistent nav structure that item 6/7/8
   pages can simply add links to later, rather than a shell that needs
   restructuring again each time a new page ships.
3. **Restyle the four screens/components that currently exist** — no
   behavior change, markup/styling only:
   - `Home.razor` — the DB-connectivity check becomes a `MudAlert` /
     loading indicator instead of plain `<p>` text.
   - `Projects.razor` — the project list becomes a `MudTable`/card layout;
     the create-project form becomes `MudTextField`s inside a `MudForm`,
     with the same `PlanningRules.ValidateProjectName` validation message
     shown via Mud's inline validation display instead of a red `<p>`.
   - `ProjectDetail.razor` — the summary counts become a `MudGrid` of
     stat cards; the Planner Grid's add-objective/add-task forms become
     `MudForm`s with `MudTextField`/`MudSelect`/`MudDatePicker`/
     `MudCheckBox`; the fixed 3-column header and per-objective/Ungrouped
     task tables get MudBlazor's table styling (see Open Questions for the
     `MudTable` vs. simpler static-table sizing call).
   - `TaskRow.razor` — status text becomes a color-coded `MudChip`/
     `MudBadge` ("status badges," as requested), and the four status
     buttons become `MudButton`s/`MudButtonGroup`, still calling the exact
     same `ChangeStatusAsync` with the exact same arguments.
4. **Document the setup in a new `README.md`** at the repo root (none
   exists today) — solution layout, how to run the app, and a dedicated
   section on the MudBlazor setup (package, service registration, theme
   customization point), as requested.

No `PlanningService`, `PlanningRules`, entity, or migration changes
anywhere in this proposal — every validation rule, computed value (e.g.
`ProjectSummary.PercentComplete`'s exact rounding), sort order, and status-
transition rule (the no-op guard, `CompletedUtc` set/clear) stays
byte-for-byte identical; only how it's *rendered* changes.

## Scope

### In Scope
- MudBlazor package + service registration + root provider components +
  render-mode setup needed for it to function (see Open Questions)
- `MainLayout.razor` → `MudLayout`/`MudAppBar`/`MudDrawer` nav shell
- Restyle: `Home.razor`, `Projects.razor` (list + create form),
  `ProjectDetail.razor` (summary cards, add-objective form, add-task form,
  Planner Grid tables), `TaskRow.razor` (status badges, status buttons)
- Loading states (e.g. "Loading…"/"Loading objectives…" text →
  `MudProgressCircular`/skeleton), empty states (e.g. "No projects yet." →
  styled empty-state, same message), and inline validation-error display —
  all re-rendered via Mud primitives, same underlying messages/conditions
- A new root `README.md` documenting the app and the MudBlazor setup
- Re-verification that every acceptance criterion from all four merged
  changes still holds functionally (not just visually) after the restyle

### Out of Scope
- **Notes/promises, meeting recording, and the accountability report** —
  none of these screens exist yet (rebuild-backlog items 6, 7, 8); nothing
  to restyle. They'll be built directly against the MudBlazor design
  system this change establishes, not restyled twice.
- **Delete-confirmation dialogs** — no delete UI exists yet anywhere
  (items 9/10 aren't built); there's nothing destructive to confirm today.
  `MudDialog`/`IDialogService` is the intended mechanism once those items
  ship — recorded as a convention for that future work, not built now.
- **Checklist tree rendering / OVERDUE / "⚑ discovered" badges** —
  rebuild-backlog item 5, not built yet; `TaskRow`'s checklist cell stays
  the same placeholder, just restyled consistently with the rest of the
  row.
- **Any change to validation rules, error message text, computed values
  (`PercentComplete`, summary counts), sort order, or the status-change
  no-op/`CompletedUtc` logic** — this is a rendering change, not a
  behavior change, anywhere in the diff.
- **A test suite** — none exists to "continue passing"; not introduced
  here either (a real testing strategy is a separate, larger decision
  beyond a UI-restyle proposal).

## Impact

- **Files affected:** ~8 (estimated) — `ManagerPlanner.Web.csproj` (new
  package ref), `Program.cs` (service registration), `App.razor`
  (CSS/JS refs + providers), `MainLayout.razor`, `Home.razor`,
  `Projects.razor`, `ProjectDetail.razor`, `TaskRow.razor`, plus a new
  root `README.md`
- **Complexity:** medium — no business-logic risk (zero `PlanningService`/
  `PlanningRules` changes), but every existing Razor file gets touched,
  and MudBlazor's interactive-render requirement (Open Questions) is a
  real, whole-app setup decision, not a per-file styling tweak
- **Risk:** low-to-medium — the domain logic is untouched, so the main
  risk is a UI-restyle accidentally changing a form binding, button
  argument, or displayed value along the way (e.g. mis-wiring a
  `MudSelect` losing the "— Ungrouped —"/"— Unassigned —" null-mapping
  `task-management` established). Mitigated by re-verifying every prior
  change's acceptance criteria functionally, not just visually, before
  this change is considered complete.

## Open Questions

1. **Restyle only what's built, or build ahead for items 6–8 too?**
   Recommended: restyle only the four existing screens now (Home,
   Projects, ProjectDetail incl. Planner Grid, TaskRow). Building UI for
   Notes/Meetings/Accountability now, with no backing `PlanningService`
   methods to call, would be speculative scaffolding this project's own
   established conventions warn against (`.specclaw/context.md`:
   "Business logic arrives with its owning backlog item, not ad hoc" —
   the same logic applies to its UI).
2. **Global `InteractiveServer` rendering vs. per-page opt-in.**
   MudBlazor's dialog/snackbar/popover providers need an interactive
   render context to function at all. Recommended: switch the whole app
   to global interactive rendering (set once, e.g. on `Routes.razor`'s
   `RouteView`), rather than continuing to add `@rendermode
   InteractiveServer` to each new page by hand — every existing feature
   page already opts in individually today, so nothing currently
   functional would change, and it removes a footgun `.specclaw/context.md`
   already flags ("forgetting this makes a page look right but silently
   do nothing on click").
3. **Table strategy for the Planner Grid.** Recommended: keep the
   existing semantic `<table>`/`<TaskRow>` structure and apply MudBlazor's
   simpler static-table CSS classes, rather than rewriting onto `MudTable`'s
   fully templated/data-bound API. The Planner Grid's rows already have
   custom interactive content (four status buttons per row) that a full
   `MudTable` rewrite would need to re-templat carefully; the simpler
   styling path gets the visual upgrade with far less restructuring risk.
   Say so if you'd rather commit to full `MudTable` now for its built-in
   sorting/filtering, even though nothing in this app needs that yet.
4. **README scope.** Since none exists, recommended: a real, if compact,
   root `README.md` (what the app is, how to run it, solution layout) with
   a dedicated MudBlazor-setup section — not just a MudBlazor snippet
   dropped with no surrounding context. Say so if you'd rather keep it
   strictly to the MudBlazor setup instructions only.

---

**To proceed:** Review this proposal and approve to begin planning.
