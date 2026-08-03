# Spec: Accountability reporting (promised-vs-delivered verdicts)

**Change:** accountability-reporting
**Created:** 2026-08-03
**Status:** 🟡 Draft

## Overview

Adds the promised-vs-delivered `Verdict` computation — domain-model.md's
DR-007, "the heart of the accountability feature" — at both scopes the
legacy apps exposed it: a single project (Executive Planning Desktop's
Accountability tab) and all projects at once (Manager Planner Desktop's
Accountability Report window). Two new `PlanningService` methods (one a
thin wrapper over the other), a new `AccountabilityRow` DTO, a new section
on `ProjectDetail.razor`, and this rebuild's first genuinely new top-level
route, `/accountability`. Also wires `TaskRow.razor`'s note-adding handler
to refresh the new section, matching a legacy fidelity requirement
functional-spec.md documents explicitly. The flagged `IsOverdue`-before-
promise precedence quirk (CQ-019) is preserved exactly, per the proposal's
recommendation and the captured golden-master fixture `GM-010`'s own
stated intent.

## Requirements

### Functional Requirements

1. **FR1 — `AccountabilityRow` class in `Reports.cs`.** Ported field-for-
   field from
   `../manager-planner/src/ExecutivePlanning.Core/Services/Reports.cs:1-49`:
   `WorkItemId`, `TaskTitle`, `ProjectName`, `AssigneeName` (default
   `"(unassigned)"`), `Status`, `Deadline`, `LatestPromisedDate`,
   `LatestPromiseText`, `LatestPromiseRecordedUtc`, `CompletedUtc`,
   `IsOverdue`, `PromiseBroken`, `PromiseKept` (all settable), and a
   read-only `Verdict` string property evaluated in this exact precedence
   order: `PromiseKept` → `"Kept promise"`; else `PromiseBroken` →
   `"BROKE promise"`; else `IsOverdue` → `"Overdue (no promise)"`; else
   `LatestPromisedDate.HasValue` → `"Promise pending"`; else
   `"On track"`.
2. **FR2 — `GetAccountabilityReportAsync(projectId)`.** Ported exactly
   from `PlanningService.cs:269-330`: loads every `WorkItem` for the
   project (`.Include(Assignee)`, `.Include(Project)`, `.Include(Notes)`),
   selects each task's latest promise note
   (`n.IsPromise && n.PromisedDate.HasValue`, `OrderByDescending(CreatedUtc)`,
   `FirstOrDefault()`), computes `IsOverdue = Deadline.HasValue &&
   Deadline.Value < now && Status != Done`, then — only when a promise
   exists — computes `PromiseKept`/`PromiseBroken`: if `Status == Done`,
   `PromiseKept = CompletedUtc.HasValue && CompletedUtc.Value.Date <=
   promised.Date` and `PromiseBroken = !PromiseKept`; otherwise
   `PromiseBroken = promised.Date < now.Date` (strict `<`, so a promise
   due exactly today is not yet broken). Returns the rows sorted
   `OrderByDescending(PromiseBroken).ThenByDescending(IsOverdue).ThenBy(Deadline
   ?? DateTime.MaxValue)`.
3. **FR3 — `GetAccountabilityForAllProjectsAsync()`.** Ported exactly from
   `PlanningService.cs:333-346`: a thin wrapper — loops every project
   (ordered by `Name`), calls FR2's method per project, concatenates, and
   re-sorts with **one extra tie-break key** the single-project sort
   doesn't have: `OrderByDescending(PromiseBroken).ThenByDescending(IsOverdue).ThenBy(ProjectName).ThenBy(Deadline
   ?? DateTime.MaxValue)`.
4. **FR4 — Preserve the `IsOverdue`-before-promise precedence quirk
   exactly.** A task whose deadline has passed but which carries an
   active, not-yet-due promise (not yet "broken") is labeled `"Overdue (no
   promise)"`, not `"Promise pending"`, exactly matching
   `.specclaw/baseline/fixtures/GM-010.json`. This is a deliberate
   fidelity decision (resolving `CQ-019` as "preserve," per the proposal's
   recommendation), not a defect to silently fix.
5. **FR5 — "Accountability" section on `ProjectDetail.razor`.** A
   read-only table — Task / Assignee / Status / Deadline / Promised /
   Verdict — populated by FR2, most-at-risk sorted first (server-side, no
   client re-sort). `Verdict` text is color-coded via MudBlazor's semantic
   `Color` enum: `Color.Success` ("Kept promise"), `Color.Error` ("BROKE
   promise"), `Color.Warning` ("Overdue (no promise)"), `Color.Info`
   ("Promise pending"), `Color.Default` ("On track") — mapping the legacy
   `AccountabilityRowVm.VerdictBrush`'s green/red/amber/blue/grey scheme
   (`RowViewModels.cs:157-162`) onto this project's existing
   `TaskRow.StatusColor`-style computed-property pattern.
6. **FR6 — New `/accountability` page (all-projects scope).** A read-only
   table — Project / Task / Assignee / Status / Deadline / Promised /
   Verdict — populated by FR3, same color-coding as FR5. A new
   `MudNavLink` is added to `MainLayout.razor`'s nav menu. No project
   filter, no refresh button — matching the legacy Manager Planner
   Desktop window exactly: "This window is read-only; no commands."
7. **FR7 — `TaskRow.razor`'s `AddNoteAsync` raises a new, parameterless
   `NoteAdded` `EventCallback`** after a successful note is saved, wired
   (in `ProjectDetail.razor`) to the page's existing full `RefreshAsync`
   — matching functional-spec.md's documented legacy behavior: "the
   command also immediately recomputes the Accountability tab's rows"
   (`MainWindowViewModel.cs:193-197`).
8. **FR8 — `ProjectDetail.razor`'s `RefreshAsync`/`OnInitializedAsync`
   also load `GetAccountabilityReportAsync` for the current project.** As
   a direct consequence, the existing `StatusChanged` callback (wired to
   the same `RefreshAsync`) now also correctly refreshes Accountability
   rows after a status change, since `Verdict` depends on
   `Status`/`CompletedUtc` too.

### Non-Functional Requirements

1. **NFR1 — DbContext lifetime.** Both new methods use
   `IDbContextFactory<PlanningDbContext>` like all fifteen existing
   `PlanningService` methods.
2. **NFR2 — Scope discipline.** After this change, none of the following
   exist anywhere in the diff: any UI control that lets a Manager act on
   or dismiss a verdict; a project-select filter or a refresh button on
   `/accountability`; a distinct "Owner" vs. "Assignee" column label
   between the two surfaces (both use `AccountabilityRow.AssigneeName`
   identically — "Assignee" is used consistently on both).
3. **NFR3 — `now` is `DateTime.UtcNow`, matching the legacy computation
   exactly.** Unlike some prior features, this is not a fidelity
   deviation — the legacy `GetAccountabilityReportAsync` already computes
   `now` as `DateTime.UtcNow` (never a local time), so no UTC-conversion
   decision is needed here.
4. **NFR4 — Reuse the existing full-refresh callback shape.** `TaskRow`'s
   new `NoteAdded` callback is wired to `ProjectDetail`'s existing full
   `RefreshAsync`, not a new lighter method — consistent with the
   `StatusChanged` callback's existing precedent (`.specclaw/context.md`:
   "Reuse the full refresh by default for this shape unless it's proven
   too expensive").

## Acceptance Criteria

Each criterion must pass for the change to be considered complete.

1. **AC1** — `dotnet build` at the solution root succeeds with 0 errors.
2. **AC2** — A row with `PromiseKept=true, PromiseBroken=true,
   IsOverdue=true` (deliberately setting the "losing" flags true too, to
   prove precedence, not just that Kept alone works) evaluates
   `Verdict == "Kept promise"` — matching `GM-008` exactly.
3. **AC3** — A row with `PromiseKept=false, PromiseBroken=true,
   IsOverdue=true` evaluates `Verdict == "BROKE promise"` — matching
   `GM-009`.
4. **AC4** — A row with `PromiseKept=false, PromiseBroken=false,
   IsOverdue=true, LatestPromisedDate=<a real future date>` evaluates
   `Verdict == "Overdue (no promise)"` — matching `GM-010`'s preserved
   quirk exactly, even though a promise is on record.
5. **AC5** — A row with `PromiseKept=false, PromiseBroken=false,
   IsOverdue=false, LatestPromisedDate=<a real future date>` evaluates
   `Verdict == "Promise pending"` — matching `GM-011`.
6. **AC6** — A row with all four flags/`LatestPromisedDate` at their
   falsy/null defaults evaluates `Verdict == "On track"` — matching
   `GM-012`.
7. **AC7** — For a `Done` task whose `CompletedUtc` date is on or before
   its latest promise's `PromisedDate` date, `GetAccountabilityReportAsync`
   yields `PromiseKept=true, PromiseBroken=false, Verdict == "Kept
   promise"` — matching `GM-013`'s exact-equality boundary (`<=`, not
   `<`).
8. **AC8** — For a `Done` task whose `CompletedUtc` date is exactly one
   day after its promise's `PromisedDate` date,
   `GetAccountabilityReportAsync` yields `PromiseKept=false,
   PromiseBroken=true, Verdict == "BROKE promise"` — matching `GM-014`.
9. **AC9** — For a non-`Done` task whose promise's `PromisedDate` date
   equals today, `PromiseBroken == false` (the comparison is strict `<`,
   not `<=` — a same-day promise is not yet broken) — matching `GM-015`.
10. **AC10** — For a non-`Done` task whose promise's `PromisedDate` date
    is exactly one day before today, `PromiseBroken == true, Verdict ==
    "BROKE promise"` — matching `GM-016`.
11. **AC11** — A task with two promise notes (an older one, `CreatedUtc`
    earlier, with a past `PromisedDate` that *would* compute as broken if
    used; a newer one, `CreatedUtc` later, with a future `PromisedDate`)
    yields `LatestPromisedDate` equal to the **newer** note's
    `PromisedDate` and `PromiseBroken == false` — the older promise is
    fully superseded, not merged or averaged — matching `GM-017`.
12. **AC12** — Two tasks in the same project sharing an identical
    `PromiseBroken`/`IsOverdue`/`Deadline` (a genuine three-key tie) are
    returned in the same relative order the legacy system actually
    produced for the equivalent shape (`GM-018`'s captured order) — no
    invented secondary sort key.
13. **AC13** — `GetAccountabilityForAllProjectsAsync`, given two projects
    whose rows would otherwise tie on `PromiseBroken`/`IsOverdue`/
    `Deadline`, orders them by `ProjectName` ascending before falling back
    to `Deadline` — confirming the extra tie-break key FR3 requires.
14. **AC14** — `ProjectDetail.razor`'s Accountability section renders one
    row per task in that project, each showing the correct `Verdict` text
    and the correct `Color` mapping (Success/Error/Warning/Info/Default
    per FR5), sorted most-at-risk-first.
15. **AC15** — The `/accountability` page renders rows spanning **every**
    project (not just one), including a `Project` column, and is reachable
    via a new nav link in `MainLayout.razor`.
16. **AC16** — Adding a note via a task's `TaskRow` causes
    `ProjectDetail`'s Accountability section to reload with updated
    verdicts, with no manual page refresh required.
17. **AC17** — No UI control anywhere lets a user act on or dismiss a
    verdict; `/accountability` has no project-select filter and no
    refresh button; both surfaces label the assignee column "Assignee"
    (never "Owner").

## Edge Cases

- **A promise note with `IsPromise=true` but `PromisedDate=null`.**
  Excluded entirely from the "latest promise" selection
  (`n.IsPromise && n.PromisedDate.HasValue`) — such a note is silently
  invisible to the accountability computation, matching the legacy filter
  exactly; not a defect.
- **A task with no promise notes at all.** `LatestPromisedDate` stays
  `null`; `Verdict` resolves to `"Overdue (no promise)"` or `"On track"`
  purely from `IsOverdue`.
- **An unassigned task.** `AssigneeName` defaults to `"(unassigned)"`,
  matching the entity default exactly — not a blank cell.
- **A task with no `Deadline`.** Sorts last within its
  `PromiseBroken`/`IsOverdue` tier via the `?? DateTime.MaxValue`
  substitution, in both the single- and all-projects sorts.
- **A project with zero tasks.** `GetAccountabilityReportAsync` returns
  an empty list; `GetAccountabilityForAllProjectsAsync` simply contributes
  no rows for that project — no error either way.
- **Genuine sort ties beyond the documented keys** (AC12) — the rebuild
  reproduces whatever stable order the legacy system's real execution
  produced, per `GM-18`'s captured order; it does not invent a documented
  tie-break the legacy source never had.

## Dependencies

- **Depends on:** `BL-003` (`WorkItem.Status`/`Deadline`/`CompletedUtc`),
  `BL-004` (`ChangeStatusAsync`'s `CompletedUtc` side effect), and `BL-007`
  (`ProgressNote.IsPromise`/`PromisedDate`/`CreatedUtc`) — all already
  built. The `Verdict` computation reads all three directly.
- **Blocks:** none — this is the last data-model-driven backlog item;
  remaining items (BL-009/010 deletion, BL-011 sample data, BL-012/013
  shell/chrome) do not depend on Accountability reporting.

## Notes

Both of the proposal's open questions are resolved here per its own
stated recommendation, not left open:

1. **The `IsOverdue`-before-promise precedence quirk (`CQ-019`) is
   preserved exactly** (FR4/AC4) — matching the captured golden-master
   fixture `GM-010`'s own stated intent to catch exactly this "fix" on
   sight. Reversing this later is a four-line change to
   `AccountabilityRow.Verdict`'s `if` ordering, not a rebuild.
2. **No refresh button on `/accountability`** (FR6/NFR2) — the legacy
   window has no commands at all; Blazor Server's own navigation already
   re-fetches on page load.
