# ADR-0005 — Fidelity / verification strategy (golden master)

**Status:** proposed
**Date:** 2026-07-27
**Deciders:** rebuild team, Manager (product owner)

## Context

The goal is a **faithful** re-implementation. SpecClaw's `verify` proves a
built feature meets *its own spec*; it does **not** prove the feature behaves
like the legacy system. The rebuild-backlog names, per item, the
behaviours that can only be pinned down against the running old app:

- **Item 8 (Accountability)** — the *"Overdue (no promise)"* precedence quirk,
  flagged as *"the single highest-priority verification input in this
  backlog… easy for a rebuild developer to 'fix' as if it were a bug."*
- **Item 1** — exact `PercentComplete` rounding/truncation.
- **Item 5** — single-subtree checklist delete (a path the legacy app never
  runs; no golden master exists — a human must define intended behaviour).
- **Item 11** — the complete `DbSeeder.cs` dataset (analysis quotes excerpts
  only).
- **Items 3/4/6** — front-end asymmetries where docs describe the difference
  but not which behaviour is canonical.
- **Various** — exact validation/error-message wording (items 6, 7).

Unlike the Delphi case, this legacy app is **.NET with named unit tests**, so
capturing golden masters is comparatively easy: it can be built and run.

## Decision drivers

- Behavioural equivalence, not just spec-conformance, is the acceptance bar.
- The legacy app and its test suite are runnable — capture is feasible now.
- Some behaviours have no legacy execution path — those need a *product
  decision*, not a capture.

## Decision

> DECIDE / adopt: establish a **golden-master capture step** run against the
> legacy app, in parallel with the rebuild:
> 1. Build and run the legacy app + its existing unit tests; record their
>    exact outputs.
> 2. For each backlog item's "Verification inputs needed," capture the named
>    input→output pairs (accountability verdicts across all precedence
>    branches; `PercentComplete` for representative counts; the full seed
>    dataset; exact error-message strings).
> 3. Store captures in the new repo (e.g. `tests/golden-master/`) and have
>    each rebuilt feature's tests assert against them.
> 4. For behaviours with **no** legacy execution path (item 5 single-subtree
>    delete; unused `ProjectStatus` values; dormant `DiscoveredInMeetingId`),
>    record an explicit **product decision** in a follow-up ADR rather than
>    "reproducing" behaviour that never ran.

## Consequences

- Golden-master capture is a **prerequisite** for meaningfully verifying items
  8, 1, 11 (and the message-wording parts of 6, 7) — start it early, not at
  the end.
- `verify` per feature checks spec-conformance; a **separate golden-master
  assertion** checks legacy equivalence. Both are required to claim "same
  app."
- Front-end asymmetries (items 3/4/6) each need a one-line product decision
  ("unify to the full form" vs "preserve the fast-add path") recorded before
  that feature is built, so the spec targets a chosen behaviour.
