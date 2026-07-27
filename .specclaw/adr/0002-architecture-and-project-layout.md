# ADR-0002 — Application architecture & project layout

**Status:** proposed
**Date:** 2026-07-27
**Deciders:** rebuild team

## Context

ADR-0001 chose Blazor. Two things now need deciding: the **Blazor hosting
model**, and the **solution/project layout** — specifically whether the legacy
`ExecutivePlanning.Core` domain/service layer is reused as-is or restructured.

`architecture.md` (L3) records a key property of the legacy design: *"every
command… calls `_service.*` directly"* with *"no repository/abstraction layer…
between the VM and the Core service."* The rebuild can keep that simple service
surface or introduce a layer; that is a deliberate call, not a default.

## Decision drivers

- Reuse the existing, test-covered `.Core` domain to preserve behaviour.
- Keep validation/business rules (`PlanningRules`) in exactly one place so
  they can't drift between old and new.
- Keep each rebuild-backlog feature independently buildable and reviewable.

## Considered options (Blazor hosting)

1. **Blazor Server** — simplest; full server-side execution, direct EF Core
   access from components, no API layer needed. Requires a live connection.
2. **Blazor WebAssembly (+ API)** — runs in the browser; needs a Web API and
   DTOs, reintroducing a boundary the legacy app never had.
3. **.NET 8 Blazor Web App (unified, per-component render modes)** — start
   server-rendered, opt components into interactivity as needed.

## Decision

> DECIDE: pick the hosting model. Recommended default for a faithful,
> low-friction port is **Blazor Server** (or the unified **Blazor Web App**
> with Server interactivity), because components can call the reused
> `PlanningService` directly — mirroring the legacy *"VM calls `_service.*`
> directly"* pattern without inventing an API tier. Choose WASM only if a
> hard requirement (offline, pure-client) demands it.

> DECIDE: project layout. Recommended:
> - `ManagerPlanner.Core` — reuse the legacy domain + EF Core model + rules
>   (ported as-is where possible).
> - `ManagerPlanner.Web` — the Blazor app referencing `.Core`.
> Keep the service surface flat (components → `PlanningService`) unless a
> concrete need justifies a repository/abstraction layer.

## Consequences

- If Blazor Server is chosen, components hold scoped `DbContext`/service
  instances; be deliberate about `DbContext` lifetime per Blazor's guidance
  (a known Blazor Server pitfall — do not share one context across a circuit
  carelessly).
- Backlog **item 0 (scaffold)** should create this solution/project skeleton
  and EF Core wiring *before* item 1 (Project management) so item 1 stays a
  pure feature change.
