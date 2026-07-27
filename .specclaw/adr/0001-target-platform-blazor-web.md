# ADR-0001 — Target platform: Blazor .NET web application

**Status:** accepted
**Date:** 2026-07-27
**Deciders:** Manager (product owner), rebuild team

## Context

The legacy system is a **.NET / Avalonia desktop** application
(`ExecutivePlanning.Core` domain + EF Core, two desktop shells: Executive
Planning Desktop and Manager Planner Desktop), per `.specclaw/analysis/
architecture.md` and `codebase-report.md`. It runs on a single user's machine
with a local SQLite database and a hand-rolled MDI window shell.

The rebuild's goal is a faithful re-implementation of the same capabilities in
a modern, maintainable, web-delivered form. A platform decision gates every
subsequent choice (stack, persistence, UI model), so it is recorded first.

## Decision drivers

- Manager's directive: deliver the rebuild as a **web application**.
- Team is a .NET shop; reusing the existing `ExecutivePlanning.Core` domain
  logic and EF Core model is high-value (the analysis quotes named unit tests
  that can be carried across).
- Desire to minimise paradigm translation — staying in .NET/C# lets the domain
  layer move largely intact, leaving mainly the UI to be re-expressed.

## Considered options

1. **Blazor (.NET web app)** — C# end to end; can reference the existing
   `.Core` domain/EF project directly; no JS/TS rewrite of business logic.
2. **ASP.NET Core Web API + separate SPA (React/Angular)** — clean API
   boundary, but duplicates model/validation logic across C# and TS, and
   introduces a second language/toolchain.
3. **ASP.NET Core MVC / Razor Pages** — server-rendered, simple, but a weaker
   fit for the rich, stateful, multi-panel UI the legacy app has.

## Decision

Build the rebuild as a **Blazor .NET web application**. It maximises reuse of
the existing C# domain and EF Core layer, keeps validation/business rules in
one language, and supports the interactive, component-based UI the app needs.

> DECIDE (feeds ADR-0002): Blazor **hosting model** — Server, WebAssembly, or
> the unified .NET 8+ **Blazor Web App** with per-component render modes. This
> is deferred to ADR-0002; note it here so it is not forgotten.

## Consequences

- The domain/service layer (`PlanningService`, `PlanningRules`, entities) is
  expected to port with minimal change; UI is the main rewrite.
- Desktop-only concerns — the MDI shell, drag/resize/tile window chrome, the
  hand-rolled `MessageBox` (backlog items 12–13) — **do not port**; they are
  re-interpreted as web navigation (see ADR-0004), not reproduced.
- Multi-user/web concerns absent from the legacy single-user desktop app
  (authentication, per-user data scoping, concurrency) become live questions;
  capture them as new ADRs if the rebuild's scope includes them.
