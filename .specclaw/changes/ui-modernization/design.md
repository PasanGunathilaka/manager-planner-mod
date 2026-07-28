# Design: UI modernization with MudBlazor

**Change:** ui-modernization
**Created:** 2026-07-28

## Technical Approach

1. **Setup** (`ManagerPlanner.Web.csproj`, `Program.cs`, `App.razor`,
   `_Imports.razor`, `MainLayout.razor`): add the `MudBlazor` package
   (unpinned — resolved to latest stable at restore time; NuGet
   reachability confirmed before writing this design), register
   `AddMudServices()`, reference MudBlazor's bundled CSS/JS, set global
   `@rendermode="InteractiveServer"` on `<HeadOutlet>`/`<Routes>`, add
   `@using MudBlazor` to `_Imports.razor`, and rebuild `MainLayout.razor`
   as a `MudLayout` (`MudAppBar` + `MudDrawer`/`MudNavMenu`) with the four
   root provider components. Once the root sets `InteractiveServer`
   globally, the now-redundant per-page `@rendermode InteractiveServer`
   directives on `Projects.razor`/`ProjectDetail.razor` are removed.
2. **Screen restyles** (`Home.razor`, `Error.razor`, `Projects.razor`,
   `ProjectDetail.razor`, `TaskRow.razor`): swap markup/controls for Mud
   equivalents, keeping every existing `@code` block's method bodies,
   event wiring (`OnValidSubmit`, `@onclick`), and `PlanningService` calls
   untouched — the one exception is `ProjectDetail.razor`'s Objective/
   Assignee `<select>`s, where `MudSelect<int?>`'s native `@bind-Value`
   replaces the manual `OnObjectiveSelected`/`OnAssigneeSelected`
   `@onchange` handlers `task-management` used (a simplification MudBlazor
   affords, not a behavior change — same fields, same `null` mapping, same
   downstream `AddTaskAsync` call).
3. **`README.md`**: a new root file documenting the app and the MudBlazor
   setup, written once the setup step (1) is done and stable.
4. **Full functional re-verification** (NFR2): after every screen is
   restyled, re-run the acceptance criteria from all four prior changes
   against the running app + direct DB inspection — the established
   verification discipline this project has used throughout, applied here
   specifically because a UI-only change touching every existing Razor
   file still carries real regression risk to `PlanningService` call
   sites and form bindings, even with zero intentional logic changes.

## Architecture

```
src/ManagerPlanner.Web/
├── ManagerPlanner.Web.csproj            + <PackageReference Include="MudBlazor" />
├── Program.cs                           + builder.Services.AddMudServices()
├── Components/
│   ├── _Imports.razor                   + @using MudBlazor
│   ├── App.razor                        + MudBlazor CSS/JS refs
│   │                                     + @rendermode="InteractiveServer" on HeadOutlet/Routes
│   ├── Layout/
│   │   └── MainLayout.razor             rebuilt: MudLayout + MudAppBar + MudDrawer/MudNavMenu
│   │                                     + MudThemeProvider/PopoverProvider/DialogProvider/SnackbarProvider
│   └── Pages/
│       ├── Home.razor                   restyled: MudAlert states
│       ├── Error.razor                  restyled: MudAlert + MudText
│       ├── Projects.razor               restyled: MudList + EditForm/MudTextField/MudButton
│       │                                 (per-page @rendermode removed — now global)
│       ├── ProjectDetail.razor          restyled: MudGrid stat cards, EditForm/Mud* controls,
│       │                                 MudSelect<int?> replacing manual @onchange,
│       │                                 MudSimpleTable wrapping existing per-objective/
│       │                                 Ungrouped <table>/<TaskRow> structure
│       │                                 (per-page @rendermode removed — now global)
│       └── TaskRow.razor                restyled: MudChip status badge, MudButtonGroup
└── README.md                            (new, repo root)
```

No changes anywhere under `src/ManagerPlanner.Core/` — `PlanningService`,
`PlanningRules`, entities, and migrations are entirely untouched.

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Web/ManagerPlanner.Web.csproj` | Modify | + `MudBlazor` package reference |
| `src/ManagerPlanner.Web/Program.cs` | Modify | + `builder.Services.AddMudServices()` |
| `src/ManagerPlanner.Web/Components/_Imports.razor` | Modify | + `@using MudBlazor` |
| `src/ManagerPlanner.Web/Components/App.razor` | Modify | + MudBlazor CSS/JS refs, + global `@rendermode="InteractiveServer"` |
| `src/ManagerPlanner.Web/Components/Layout/MainLayout.razor` | Modify | Rebuilt as `MudLayout` shell + root Mud providers |
| `src/ManagerPlanner.Web/Components/Pages/Home.razor` | Modify | `MudAlert` states, same `_canConnect` logic |
| `src/ManagerPlanner.Web/Components/Pages/Error.razor` | Modify | `MudAlert`/`MudText`, same `RequestId`/dev-mode content |
| `src/ManagerPlanner.Web/Components/Pages/Projects.razor` | Modify | `MudList`, `MudTextField`/`MudButton` inside existing `EditForm` |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Modify | `MudGrid` stat cards, Mud form controls, `MudSelect<int?>`, `MudSimpleTable` |
| `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor` | Modify | `MudChip` status badge, `MudButtonGroup` status buttons |
| `README.md` | Create | App overview, run instructions, MudBlazor setup section |

## Data Model Changes

None. No entity, schema, or migration changes anywhere in this change.

## API Changes

None. No HTTP/JSON API — Mud components call `PlanningService` directly
through the exact same methods every prior change already established.

## Key Decisions

1. **Keep `<EditForm>`/`OnValidSubmit` wiring; swap only the input
   controls.** All three existing forms (add-project, add-objective,
   add-task) keep their current `EditForm`/`OnValidSubmit="..."`
   structure — only `<InputText>`/`<select>`/raw `<button>` become
   `MudTextField`/`MudSelect`/`MudDatePicker`/`MudCheckBox`/`MudButton`.
   This preserves every existing submit-handler and `ValidationException`
   catch path unchanged, minimizing regression risk in a change whose
   entire point is "look different, behave identically."
2. **`MudSelect<int?>` replaces the manual `@onchange` parsing** on the
   Objective/Assignee dropdowns. `task-management` used explicit
   `OnObjectiveSelected`/`OnAssigneeSelected` handlers specifically
   because plain HTML `<select>` binding to a nullable `int?` wasn't
   verified reliable at the time; `MudSelect<T>` natively supports
   `@bind-Value` for any `T` including nullable value types, so this is a
   legitimate simplification, not a workaround — same `null`-mapping
   behavior, fewer lines.
3. **Keep the existing `<table>`/`TaskRow` structure; wrap in
   `MudSimpleTable`, not a full `MudTable` rewrite.** `MudSimpleTable` is
   a lightweight MudBlazor component that applies consistent table
   styling to markup you still author yourself — it doesn't require
   restructuring `TaskRow`'s row-per-task pattern (with its embedded
   status buttons) into `MudTable`'s templated/data-bound API. Resolves
   proposal Open Question 3 in favor of the lower-risk option.
4. **Global `InteractiveServer` rendering, set once in `App.razor`.**
   MudBlazor's `MudDialogProvider`/`MudPopoverProvider`/
   `MudSnackbarProvider` require an interactive render context; every
   existing feature page already opted into `InteractiveServer`
   individually, so this changes nothing functionally while removing the
   per-page opt-in footgun `.specclaw/context.md` already flagged.
5. **No external CDN reference (e.g. Google Fonts) added.** MudBlazor's
   bundled CSS provides system-font fallbacks without it, and icons
   render as inline SVG via `Icons.Material.*` constants (no icon-font
   download needed). Keeps this rebuild's local-first character —
   nothing else in the app calls out to an external network service
   either (SQLite is a local file).
6. **`MudBlazor` package version left unpinned in this design.** NuGet
   reachability was confirmed live before writing this plan; letting
   `dotnet add package`/restore resolve the latest stable version avoids
   hardcoding a version number that can't be verified against a live feed
   from inside this design step.
7. **Restyle `Error.razor` too, beyond the proposal's explicit four-file
   list.** It's a currently-existing page whose `class="text-danger"` has
   no effect today (no CSS framework defines that class) — directly in
   scope of the proposal's own "error handling" goal, and a small, safe
   addition consistent with the rest of this change.

## Grounding sources

- `.specclaw/analysis/architecture.md` — confirms the legacy MDI shell is
  intentionally *not* ported (ADR-0004), reinforcing that `MainLayout`'s
  redesign here is genuinely new web-native chrome, not a port.
- `.specclaw/adr/0004-mdi-shell-to-web-navigation.md` — "map the MDI
  shell to web-native navigation... routed pages or a panel/tab layout" —
  grounds the `MudAppBar`/`MudDrawer` shell as the web-native
  continuation of that decision, sized for future item 6/7/8 pages to add
  nav links to.
- `.specclaw/context.md` — "Feature pages needing interactivity... MUST
  declare `@rendermode InteractiveServer` explicitly... forgetting this
  makes a page look right but silently do nothing on click" (grounds Key
  Decision 4's move to global rendering) and "Do not add `PlanningService`
  or any feature UI casually... Business logic arrives with its owning
  backlog item, not ad hoc" (grounds NFR4/AC11's scope discipline — the
  same logic extended to UI for not-yet-built features).
- **Live verification**, not just documentation: confirmed via direct
  repo inspection (not assumed) that no CSS/component framework is
  currently referenced anywhere in `ManagerPlanner.Web` (`grep` for
  `MudBlazor|Bootstrap|Blazorise` across `.csproj`/`.razor`/`.json`
  returned nothing), that no `README.md` exists anywhere in the repo
  (`git ls-files | grep -i readme` only matches the unrelated
  `.specclaw/adr/README.md`), and that outbound NuGet access is live
  (`https://api.nuget.org/v3/index.json` returned `200`).

## Risks & Mitigations

- **Risk:** a restyle accidentally changes a form binding, button
  argument, or displayed value while swapping controls (e.g. losing the
  "— Ungrouped —" → `null` mapping when moving off the manual `@onchange`
  handler). **Mitigation:** NFR2's full functional re-verification pass,
  explicitly re-checking every prior change's acceptance criteria against
  the running app + direct DB inspection, not just visual review.
- **Risk:** global `InteractiveServer` rendering introduces a
  circuit/connection-state issue on a page that previously rendered
  statically (`Home.razor`). **Mitigation:** `Home.razor` has no
  interactive controls today, so there's no existing behavior to break;
  AC4 explicitly re-confirms its three states still render correctly
  under the new render mode.
- **Risk:** `MudSimpleTable`'s styling doesn't play well with `TaskRow`'s
  existing `<tr>`/`<td>` markup once wrapped. **Mitigation:**
  `MudSimpleTable` is designed exactly for "style my own `<table>`
  markup" use cases; if a specific styling conflict surfaces during
  build, the fallback is plain MudBlazor CSS utility classes on the
  existing `<table>` directly, not a MudTable rewrite (still consistent
  with Key Decision 3).
- **Risk:** scope creep toward "modernizing" the checklist placeholder,
  OVERDUE badges, or other not-yet-built item-5 content, since they sit
  visually right next to what's being restyled. **Mitigation:** AC11
  explicitly checks that no such content exists in the diff; the
  checklist cell gets styling consistency only, no new content.
