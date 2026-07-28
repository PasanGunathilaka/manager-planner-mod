# Tasks: UI modernization with MudBlazor

**Change:** ui-modernization
**Created:** 2026-07-28
**Total Tasks:** 5

## Summary

Five tasks across three waves: (1) MudBlazor setup + app shell + README,
(2) three parallel screen restyles (Home/Error, Projects, ProjectDetail/
TaskRow), (3) full functional re-verification of every prior change's
acceptance criteria against the restyled app. No task touches
`PlanningService`/`PlanningRules`/entities/migrations, and no task adds
Notes/Meeting/Accountability/delete UI or checklist/badge content — those
stay out of scope per spec.md NFR1/NFR4/AC10/AC11.

## Tasks

### Wave 1 — Setup

- [x] `T1` — MudBlazor package, service registration, app shell, README
  - Files: `src/ManagerPlanner.Web/ManagerPlanner.Web.csproj`, `src/ManagerPlanner.Web/Program.cs`, `src/ManagerPlanner.Web/Components/_Imports.razor`, `src/ManagerPlanner.Web/Components/App.razor`, `src/ManagerPlanner.Web/Components/Layout/MainLayout.razor`, `README.md`
  - Estimate: medium
  - Depends: none
  - Notes: Run `dotnet add src/ManagerPlanner.Web/ManagerPlanner.Web.csproj package MudBlazor` (no version pin — let restore resolve latest stable; NuGet reachability already confirmed live). In `Program.cs`, add `builder.Services.AddMudServices();` alongside the existing service registrations. In `_Imports.razor`, add `@using MudBlazor`. In `App.razor`, add `<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />` in `<head>` and `<script src="_content/MudBlazor/MudBlazor.min.js"></script>` before/after the existing `blazor.web.js` script tag (whichever order MudBlazor's own docs specify at build time — check the installed package's own README/sample if unsure); set `@rendermode="InteractiveServer"` on both `<HeadOutlet>` and `<Routes>` (design.md Key Decision 4 — global interactive rendering, not per-page). **No external CDN link (e.g. Google Fonts) — design.md Key Decision 5.** Rebuild `MainLayout.razor` as a `MudLayout`: a `MudAppBar` with the app title, a `MudDrawer` containing a `MudNavMenu` with `MudNavLink`s to `/` ("Home") and `/projects` ("Projects"), a `MudMainContent`/`MudContainer` wrapping `@Body`, plus `<MudThemeProvider />`, `<MudPopoverProvider />`, `<MudDialogProvider />`, `<MudSnackbarProvider />` once at the top (these must exist exactly once, app-wide — AC2). Write `README.md` at the repo root (none exists — confirmed via `git ls-files | grep -i readme`): what the app is, .NET 8 SDK prerequisite, how to run it (`dotnet run --project src/ManagerPlanner.Web`), the two-project solution layout, and a "UI Framework" section covering the MudBlazor package/`AddMudServices()`/where the providers live/how to customize the theme (AC12). Verify: `dotnet build` succeeds (AC1); the app loads with no JS console errors and the nav links in the new drawer work (AC2, AC3).

### Wave 2 — Screen restyles

- [x] `T2` — Restyle Home.razor and Error.razor
  - Files: `src/ManagerPlanner.Web/Components/Pages/Home.razor`, `src/ManagerPlanner.Web/Components/Pages/Error.razor`
  - Estimate: small
  - Depends: `T1`
  - Notes: `Home.razor` — replace the three plain `<p>` states with `<MudAlert Severity="Severity.Info">`/`<MudAlert Severity="Severity.Success">`/`<MudAlert Severity="Severity.Error">` matching the existing `_canConnect is null` / `== true` / else conditions exactly — don't touch `OnInitializedAsync` or the `DbFactory`/`CanConnectAsync` call. `Error.razor` — replace `<h1 class="text-danger">Error.</h1>`/`<h2 class="text-danger">...</h2>` with a `MudAlert Severity="Severity.Error"` carrying the same text, and restyle the Request ID / Development-mode paragraphs with `MudText` — keep the `[CascadingParameter] HttpContext`/`RequestId`/`ShowRequestId` code exactly as-is. Verify: both pages render the correct state/content with no console errors (AC4 for Home; visual-only check for Error, no AC number of its own since it wasn't in the original screen list but is grounded in design.md Key Decision 7).

- [x] `T3` — Restyle Projects.razor
  - Files: `src/ManagerPlanner.Web/Components/Pages/Projects.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: Remove the now-redundant `@rendermode InteractiveServer` line (global rendering is set in `App.razor` by `T1`). Replace the `<ul><li><a href=...>` project list with a `MudList`/`MudListItem` per project — each item's `Href` set to `/projects/{id}`, same Name/Description text, same "No projects yet." empty state and "Loading projects…" loading state (as `MudAlert`s or `MudText`, matching the existing `_projects is null`/`.Count == 0` conditions exactly). Keep `<EditForm Model="this" OnValidSubmit="AddProjectAsync">` as-is; swap `<InputText @bind-Value="_newName" />`/`<InputText @bind-Value="_newDescription" />` for `<MudTextField @bind-Value="_newName" Label="Name" />`/`<MudTextField @bind-Value="_newDescription" Label="Description" />`, and `<button type="submit">Add project</button>` for `<MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">Add project</MudButton>`. The existing inline `<p style="color: red;">@_errorMessage</p>` becomes a `MudAlert Severity="Severity.Error"` shown under the same `!string.IsNullOrEmpty(_errorMessage)` condition. Do not touch `AddProjectAsync`'s body or the `ValidationException` catch. Verify: submitting a valid name still creates a project and clears the form (re-confirms `project-management` AC2); submitting an empty/overlong name still shows `PlanningRules.ValidateProjectName`'s exact message and creates no row (re-confirms AC3) — spec.md AC5.

- [x] `T4` — Restyle ProjectDetail.razor and TaskRow.razor
  - Files: `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`, `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor`
  - Estimate: large
  - Depends: `T1`
  - Notes: This is the largest task — the Planner Grid, both add-forms, and the task-row component all live here.

    **`ProjectDetail.razor`**: remove the now-redundant `@rendermode InteractiveServer` line. Replace the `<ul><li>` summary block with a `MudGrid` of `MudPaper`/stat-tile cells for Total/Done/In progress/Blocked/Not started/Overdue/% complete — same values from the same `_summary` fields, no new computation. Replace `<button @onclick="RefreshAsync">Refresh</button>` with `<MudButton OnClick="RefreshAsync" StartIcon="@Icons.Material.Filled.Refresh">Refresh</MudButton>`. Keep both `<EditForm>`s (`AddObjectiveAsync`, `AddTaskAsync`) exactly as-is; swap their controls: add-objective's `<InputText>` → `MudTextField`; add-task's Title `<InputText>` → `MudTextField`, Deadline `<InputDate>` → `MudDatePicker<DateTime?>` bound the same way, Description `<InputTextArea>` → `MudTextField` with `Lines="3"`, Discovered `<InputCheckbox>` → `MudCheckBox<bool>`. **Replace the Objective and Assignee `<select>` + `OnObjectiveSelected`/`OnAssigneeSelected` `@onchange` handlers with `MudSelect<int?>` bound via `@bind-Value` directly to `_newTaskObjectiveId`/`_newTaskAssigneeId`** (design.md Key Decision 2) — first `<MudSelectItem Value="@((int?)null)">— Ungrouped —</MudSelectItem>`/`— Unassigned —` item, then one `<MudSelectItem>` per objective/team member, same as today's options; delete the two now-unused handler methods. Both forms' inline error `<p style="color: red;">` become `MudAlert Severity="Severity.Error"`. Wrap the fixed 3-column header and every per-objective/Ungrouped task table in `<MudSimpleTable>` (design.md Key Decision 3) — keep the exact same `<thead>`/`<tbody>`/`<TaskRow>` structure inside it, don't restructure into `MudTable`'s templated API. Section headings (`<h2>`/`<h3>`) become `MudText` with appropriate `Typo`; "No objectives yet."/"No tasks yet." empty states become `MudText`/`MudAlert` with the identical message text and identical visibility conditions.

    **`TaskRow.razor`**: add a `StatusColor` computed property (`WorkItemStatus.NotStarted` → `Color.Default`, `InProgress` → `Color.Info`, `Blocked` → `Color.Error`, `Done` → `Color.Success`) alongside the existing `StatusText`; render status as `<MudChip Color="@StatusColor" Size="Size.Small">@StatusText</MudChip>`. Replace the four plain `<button type="button">` status controls with a `MudButtonGroup` of `MudButton`s, each still calling the existing `SetStatusAsync(WorkItemStatus.X)` handler unchanged. Do not touch `SetStatusAsync`'s body (still `GetCurrentManagerIdAsync()` → `ChangeStatusAsync(...)` → `StatusChanged.InvokeAsync()`).

    Verify (spec.md AC6, AC7, AC8, AC9): adding an objective still validates/append-sorts correctly; adding a task via the restyled form (including the new `MudSelect<int?>` bindings) still calls `AddTaskAsync` with the same argument semantics — confirm the "— Ungrouped —"/"— Unassigned —" → `null` mapping survives the binding-mechanism change (spec.md's flagged edge case) via direct DB inspection; clicking a status button still creates the correct `StatusChange` row/`CompletedUtc` behavior and the no-op guard still produces zero new rows on a repeated same-status click; the summary auto-refreshes after a status change; each row's status renders as a distinctly colored `MudChip`.

### Wave 3 — Final re-verification

- [x] `T5` — Full functional re-verification across all restyled screens
  - Files: none (verification only — no source changes)
  - Estimate: medium
  - Depends: `T2`, `T3`, `T4`
  - Notes: With every screen restyled, re-run the acceptance criteria from all four prior changes' `spec.md` files end-to-end against the running app, combined with direct SQLite inspection where prior verifications used it (`project-management`'s create/summary flow, `planner-grid`'s add-objective/`SortOrder` flow, `task-management`'s add-task/ungrouped/validation flow, `task-status-transitions`'s status-button/no-op/`CompletedUtc` flow) — spec.md NFR2. Also confirm: pre-existing data from earlier verification sessions (the "ffg" project's `Objective A`/`Full form task`/`Ungrouped task` rows) still renders correctly under the new UI (spec.md's edge case); no external CDN reference exists in `App.razor` (AC13); no Notes/Meeting/Accountability/delete UI exists anywhere in the diff (AC11). Use `form_input`/JS dispatch (`element.click()`) per `.specclaw/context.md`'s documented fallback if real mouse-click dispatch via claude-in-chrome is wedged again, as it has been for four changes running; fall back to a scratch console app calling `PlanningService` in-process against the live SQLite file if browser evidence alone is in doubt.

---

## Legend

- `[ ]` Pending
- `[~]` In Progress
- `[x]` Complete
- `[!]` Failed

**Task format:**
```
- [ ] `T<n>` — <title>
  - Files: <files to create/modify>
  - Estimate: small | medium | large
  - Depends: <task ids> (if any)
  - Notes: <additional context>
```
