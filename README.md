# Manager Planner

A Blazor web rebuild of a legacy Avalonia desktop app for managers who plan
projects, break them into tasks assigned to team members, and track work
status through to completion.

## Prerequisites

- .NET 8 SDK

## Running the app

```bash
dotnet run --project src/ManagerPlanner.Web
```

The app applies pending EF Core migrations and bootstraps a single Manager
user on first run, then serves at the URL printed in the console (e.g.
`http://localhost:5127`).

## Solution layout

- **`src/ManagerPlanner.Core`** — the domain/persistence layer: entities
  (`Domain/`), validation rules (`Validation/PlanningRules.cs`), the EF
  Core `DbContext` and migrations (`Data/`, `Migrations/`), and business
  logic (`Services/PlanningService.cs`).
- **`src/ManagerPlanner.Web`** — the Blazor Server app. References `.Core`
  directly; components call `PlanningService` with no separate API layer.

## UI framework

The app uses [MudBlazor](https://mudblazor.com/) for its component library
and styling.

- **Package:** `MudBlazor` (`ManagerPlanner.Web.csproj`).
- **Service registration:** `builder.Services.AddMudServices();` in
  `Program.cs`.
- **Setup:** `App.razor` references MudBlazor's bundled CSS/JS
  (`_content/MudBlazor/MudBlazor.min.css` / `.min.js`) and sets
  `@rendermode="InteractiveServer"` globally on `<HeadOutlet>`/`<Routes>` —
  MudBlazor's dialog, popover, and snackbar providers require an
  interactive render context to function.
- **Providers:** `MudThemeProvider`, `MudPopoverProvider`,
  `MudDialogProvider`, and `MudSnackbarProvider` live once, app-wide, in
  `Components/Layout/MainLayout.razor`.
- **Customizing the theme:** pass a `Theme` parameter to
  `<MudThemeProvider Theme="@myTheme" />` in `MainLayout.razor` — see
  MudBlazor's [theming documentation](https://mudblazor.com/customization/overview)
  for the `MudTheme` API.
