# ADR-0003 — Persistence & schema-evolution strategy

**Status:** proposed
**Date:** 2026-07-27
**Deciders:** rebuild team

## Context

`codebase-report.md` (Risks/Tech-Debt) flags, and rebuild-backlog item 1
repeats: *"No EF Core migrations exist anywhere in the repo…
`PlanningDbContextFactory.Create` only calls `ctx.Database.EnsureCreated()`."*
The legacy app also uses **separate SQLite database files per app version**
(item 2's note: *"v2 has extra tables (Objectives, ChecklistItems, TaskOwners),
so it uses its own database file"*).

So there is **no migration history to observe** — the rebuild must choose its
own schema-evolution strategy rather than reproduce one.

## Decision drivers

- The rebuild is web-hosted and expected to evolve; `EnsureCreated()` (no
  migrations) does not support schema change over time.
- Domain/EF model is being reused, so the entity shapes are known and stable.
- Faithful data behaviour matters (the app is an accountability tool).

## Considered options

1. **EF Core Migrations from day one** — proper `Add-Migration` history;
   supports evolution; the conventional choice for an app expected to grow.
2. **`EnsureCreated()` (as legacy)** — simplest, but a dead end for schema
   change; only viable for a throwaway/demo.
3. **Database-first / SQL scripts** — unnecessary given a code-first model
   already exists.

## Decision

> DECIDE: database engine — keep **SQLite** (matches legacy, simplest) or move
> to **SQL Server / PostgreSQL** for a multi-user web deployment. Recommended:
> confirm against ADR-0001's multi-user question — a shared web app usually
> wants a server database, but SQLite is fine for a single-tenant pilot.

> DECIDE (recommended = option 1): adopt **EF Core Migrations** from the first
> scaffold. Do NOT carry over `EnsureCreated()`. This is the schema-strategy
> call item 1 requires.

## Consequences

- Backlog **item 0 (scaffold)** creates the initial migration for the reused
  entity model.
- No data-migration path from the legacy SQLite files is assumed here; if
  existing legacy data must be imported, that is a **separate ADR + backlog
  item** (the analysis does not fully document the on-disk schema).
- Item 11 (sample-data lifecycle) still needs the **full `DbSeeder.cs` dataset
  exported from the legacy app** — the analysis quotes only excerpts. That is a
  golden-master input, tracked in ADR-0005, not something this ADR resolves.
