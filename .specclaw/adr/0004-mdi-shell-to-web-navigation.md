# ADR-0004 — Desktop MDI shell → web navigation model

**Status:** proposed
**Date:** 2026-07-27
**Deciders:** rebuild team, Manager (product owner)

## Context

The legacy Manager Planner Desktop uses a **hand-rolled MDI shell** — backlog
item 12 (`MdiHost`/`MdiWindow`, drag/reposition, maximize/restore, tile/cascade,
minimise-hides-window) and item 13 (Exit, static About). `codebase-report.md`
describes this as *"bespoke UI-framework code with no test safety net… hand-roll
modal dialogs and MDI window chrome."* `functional-spec.md` Named Gap #7 notes
minimised windows have *"no taskbar-equivalent."*

None of this is a domain feature — `domain-model.md` has no entity, rule, or
enum for window chrome. In a **web app these concepts do not exist**: there are
no draggable child windows, no tile/cascade, no per-window minimise. So items
12–13 cannot be "ported"; they must be **re-interpreted**.

## Decision drivers

- Preserve the *capabilities* the shell hosted (Projects, Planner Grid,
  Task+Notes, Accountability views) without reproducing desktop windowing.
- Use web-native patterns users already understand (routing, tabs, dialogs).
- Avoid rebuilding bespoke, untested window-management code in a new stack.

## Considered options

1. **Web-native navigation** — Blazor routing + a nav layout; the four
   "windows" become **pages or panels/tabs**; modals become standard dialog
   components; Exit/About become a menu + an About page/dialog.
2. **Emulate MDI in the browser** — recreate draggable/resizable child windows
   with a JS/Blazor windowing library. High effort, reproduces a flagged
   tech-debt pattern, non-idiomatic for web.

## Decision

> DECIDE (recommended = option 1): map the MDI shell to **web-native
> navigation**. The Projects, Planner Grid, Task+Notes, and Accountability
> windows (items 1, 2, 3/6/7, 8) become routed pages or a panel/tab layout;
> browser-native/Blazor dialogs replace the hand-rolled `MessageBox`; the
> Window menu (cascade/tile/show) is dropped as not-applicable and recorded as
> an intentional non-port.

## Consequences

- Backlog **items 12 and 13 are re-scoped from "rebuild" to "web-reinterpret /
  drop"** — do not create features that reproduce drag/resize/tile. Note this
  explicitly when you `propose` them (or fold their surviving parts — an
  About page, an app layout — into the scaffold / relevant feature).
- Named Gap #7 (no taskbar for minimised windows) becomes moot under web
  navigation; record that the limitation is dissolved, not preserved.
- Any behaviour genuinely worth keeping (e.g. which views exist and how they
  interlink) is captured by the page/nav design, grounded in
  `functional-spec.md`'s Workflows.
