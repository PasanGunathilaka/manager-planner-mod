# Design: Project management — create, browse, switch, and summarize projects

**Change:** project-management
**Created:** 2026-07-27

## Technical Approach

1. Add `PlanningService` to `ManagerPlanner.Core/Services/`, ported from
   the real legacy `Services/PlanningService.cs`'s Project-related methods
   — but constructed with `IDbContextFactory<PlanningDbContext>` instead
   of a directly injected `PlanningDbContext`, per this project's
   established Blazor Server DbContext-lifetime pattern. Each method opens
   and disposes its own short-lived context.
2. Add `ProjectSummary` to `ManagerPlanner.Core/Services/Reports.cs`
   (matching the legacy filename; item 8 will later add
   `AccountabilityRow` to the same file), field-for-field identical to the
   legacy DTO, including the exact `PercentComplete` formula.
3. Add one new method with no legacy equivalent,
   `GetCurrentManagerIdAsync()`, plus a startup bootstrap in `Program.cs`
   that guarantees exactly one `User` with `Role = Manager` exists —
   standing in for the auth/seeding this app doesn't have yet.
4. Add two Blazor pages (`Projects.razor`, `ProjectDetail.razor`), both
   `@rendermode InteractiveServer`, replacing the legacy windows/tabs per
   ADR-0004's web-navigation direction. "Switching" the active project is
   simply navigating between `/projects/{id}` rows.
5. Add a nav link to the (currently bare) `MainLayout.razor`.

No entity, schema, or migration changes — this change is pure
service+UI on top of the existing model.

## Architecture

```
src/ManagerPlanner.Core/
└── Services/                          (new folder)
    ├── PlanningService.cs             GetProjectsAsync, AddProjectAsync,
    │                                   GetProjectSummaryAsync, GetCurrentManagerIdAsync
    └── Reports.cs                     ProjectSummary (PercentComplete computed)

src/ManagerPlanner.Web/
├── Program.cs                         + AddScoped<PlanningService>(), + Manager bootstrap
└── Components/
    ├── _Imports.razor                 + @using ManagerPlanner.Core.Domain / .Services
    ├── Layout/MainLayout.razor        + nav link to /projects
    └── Pages/
        ├── Projects.razor             (new) /projects — browse + create
        └── ProjectDetail.razor        (new) /projects/{id:int} — summary + refresh
```

`PlanningService` is the only new dependency `ManagerPlanner.Web` takes on
beyond what item 0 already wired — no new package references, no API/DTO
boundary (pages call `PlanningService` directly), per ADR-0002.

## File Changes Map

| File | Action | Description |
|------|--------|-------------|
| `src/ManagerPlanner.Core/Services/PlanningService.cs` | Create | `GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`, `GetCurrentManagerIdAsync` — all via `IDbContextFactory<PlanningDbContext>` |
| `src/ManagerPlanner.Core/Services/Reports.cs` | Create | `ProjectSummary` DTO, `PercentComplete` computed exactly as legacy |
| `src/ManagerPlanner.Web/Program.cs` | Modify | Register `PlanningService` (Scoped); startup bootstrap ensuring one `Role = Manager` `User` exists |
| `src/ManagerPlanner.Web/Components/Pages/Projects.razor` | Create | `/projects` — browse + create, `@rendermode InteractiveServer` |
| `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor` | Create | `/projects/{id:int}` — summary + refresh, `@rendermode InteractiveServer` |
| `src/ManagerPlanner.Web/Components/Layout/MainLayout.razor` | Modify | Add nav link to `/projects` |
| `src/ManagerPlanner.Web/Components/_Imports.razor` | Modify | Add `@using ManagerPlanner.Core.Domain` / `ManagerPlanner.Core.Services` for feature pages |

## Data Model Changes

None. This change reuses the entity model and schema from
`scaffold-blazor-solution` exactly as-is — no new fields, entities, or
migrations.

## API Changes

None. No HTTP/JSON API is introduced — pages call `PlanningService`
directly as in-process C#, consistent with ADR-0002's decision not to
introduce an API/DTO boundary the legacy app never had.

## Key Decisions

1. **`PlanningService(IDbContextFactory<PlanningDbContext>)`, not
   `PlanningService(PlanningDbContext)`** — the legacy constructor takes a
   `PlanningDbContext` directly (`public PlanningService(PlanningDbContext
   db) => _db = db;`), but this project already established (item 0) that
   Blazor Server components must not hold a single shared `DbContext`
   across a circuit's lifetime. Each `PlanningService` method opens its
   own context via the factory instead.
2. **`GetCurrentManagerIdAsync()` + startup Manager bootstrap** — not a
   legacy port. The legacy apps resolved "the current user" once at
   ViewModel startup (`users.FirstOrDefault(u => u.Role ==
   UserRole.Manager)?.Id`) against a `DbSeeder`-populated database; this
   app has neither login nor seed data yet. This is a deliberate,
   minimal stand-in — flagged in the proposal's Open Questions and
   carried forward here — not a step toward building real
   authentication now.
3. **`/projects/{id}` treats an empty-name-and-zero-counts summary as
   "not found."** The legacy desktop apps could never reach an invalid
   project id (always selected from a live `ListBox`/`ComboBox`); a
   URL-addressable web page can, so this is new edge-case handling the
   web port requires that legacy never needed. `GetProjectSummaryAsync`
   itself is left exactly as legacy (no thrown exception for a missing
   project) — the "not found" handling lives in the page, not the service.
4. **`@rendermode InteractiveServer` on both new pages** — required for
   `@onclick` handlers (Add project, Refresh) to work; the Blazor Web App
   template's default static server rendering only re-renders on
   navigation/form-post, not arbitrary button clicks.
5. **`ProjectSummary` lives in `Reports.cs`**, matching the legacy
   filename, even though only one DTO exists there for now — item 8 will
   add `AccountabilityRow` to the same file rather than inventing a
   different split.

## Grounding sources

- `.specclaw/analysis/domain-model.md` — `Project` entity fields, Business
  Rule 1 ("Project name required, ≤120 chars —
  `PlanningRules.ValidateProjectName`"), and the `ProjectSummary` derived
  read-model description ("carries per-project task counts... and a
  computed `PercentComplete`").
- **Real legacy source** (`C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs`
  and `Services/Reports.cs`) — read directly, not just the doc summary,
  confirming the exact `GetProjectsAsync`/`AddProjectAsync` query shapes
  and the exact `PercentComplete` formula: `TotalTasks == 0 ? 0 :
  Math.Round(100.0 * Done / TotalTasks, 1)`. This resolves
  rebuild-backlog.md item 1's flagged "golden-master capture" need for
  `PercentComplete` — the formula is now confirmed from source, not
  guessed.
- `.specclaw/adr/0002-architecture-and-project-layout.md` — "be
  deliberate about `DbContext` lifetime per Blazor's guidance" (grounds
  Key Decision 1) and the flat-service-surface, no-API-boundary guidance.
- `.specclaw/adr/0004-mdi-shell-to-web-navigation.md` — "map the MDI shell
  to web-native navigation... become routed pages" (grounds the
  `/projects` + `/projects/{id}` page structure).
- `.specclaw/adr/0001-target-platform-blazor-web.md` — "Multi-user/web
  concerns... become live questions; capture them as new ADRs if the
  rebuild's scope includes them" (grounds Key Decision 2's framing as a
  stand-in, not a real auth decision).
- `.specclaw/context.md` — "Every future component that touches the
  database should inject the factory and create/dispose a short-lived
  context per operation" (directly actioned by Key Decision 1).

## Risks & Mitigations

- **Risk:** the Manager-bootstrap addition could be mistaken for scope
  creep beyond a pure port. **Mitigation:** explicitly proposed and
  flagged as Open Question 1 in `proposal.md`, and called out again here
  and in spec.md's Notes as a deliberate, temporary stand-in.
- **Risk:** `PercentComplete`'s rounding (`Math.Round` uses
  banker's/to-even rounding by default) could differ from a naive
  "round half up" assumption at `.x5` boundaries. **Mitigation:** use the
  identical 2-argument `Math.Round(value, 1)` call the legacy code uses —
  no `MidpointRounding` override, since legacy doesn't specify one either.
- **Risk:** `AC7` (Refresh button reflects changed data) can't be
  exercised through any UI yet, since task creation/status-change is item
  3/4. **Mitigation:** verify by inserting/updating a `WorkItem` row
  directly via EF Core or a scratch script — called out explicitly in
  spec.md's Edge Cases so it isn't mistaken for a missing feature.
- **Risk:** forgetting `@rendermode InteractiveServer` would silently
  produce pages whose buttons don't respond to clicks (a common Blazor
  Web App gotcha). **Mitigation:** called out explicitly in NFR2 and Key
  Decision 4; verified manually during build (AC3/AC7 require working
  buttons, not just correct markup).
