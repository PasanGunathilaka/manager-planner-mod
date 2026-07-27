# Architecture Decision Records — Manager Planner (Web Rebuild)

These ADRs record the forward-looking decisions for rebuilding the legacy
ExecutivePlanning / ManagerPlanner desktop app as a **Blazor .NET web
application**. They are grounded in `.specclaw/analysis/` (the analysis of the
old app) but describe the *new* system — the choices static analysis could not
make.

Status legend: `proposed` · `accepted` · `superseded`

| # | Decision | Status |
|---|----------|--------|
| 0001 | Target platform — Blazor .NET web app | accepted |
| 0002 | Application architecture & project layout | proposed |
| 0003 | Persistence & schema-evolution strategy | proposed |
| 0004 | Desktop MDI shell → web navigation model | proposed |
| 0005 | Fidelity / verification strategy (golden master) | proposed |

> Each ADR records **one** decision with the options that were rejected, so a
> later reader understands *why*, not just *what*. Supersede rather than edit
> once a decision is accepted and later changed.
