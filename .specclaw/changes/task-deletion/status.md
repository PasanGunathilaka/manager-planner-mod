# Status: Task deletion (cascade)

**Change:** task-deletion
**Started:** 2026-08-03
**Last Updated:** 2026-08-03

## Progress

| Phase | Status | Notes |
|-------|--------|-------|
| Proposal | 🟢 Approved | Approved by proceeding to `/specclaw:plan` |
| Spec | 🟢 Complete | 5 FRs, 3 NFRs, 8 ACs, 4 edge cases |
| Design | 🟢 Complete | 1 new `PlanningService` method, `TaskRow` gains an Actions cell (Delete + confirm dialog) and a `TaskDeleted` callback |
| Tasks | 🟢 Complete | 2 tasks / 2 waves |
| Build | 🟢 Complete | Both tasks done; merged to master |
| Verify | ⚪ Pending | Run `/specclaw:verify` next |

## Task Progress

**Completed:** 2 / 2
**Failed:** 0

Both tasks complete. `dotnet build` succeeds (0 errors). T1 (applied
directly) initially followed the proposal's planned verbatim port
(`FindAsync` + `Remove` + `SaveChangesAsync`), but a live end-to-end
verification attempt against the real dev DB threw `SQLite Error 19:
FOREIGN KEY constraint failed` on any task with a nested (parent+child)
checklist item — exactly the primary scenario `GM-025`/AC2 requires. Root
cause confirmed by direct reproduction (including against the real legacy
`PlanningService` class run through an equivalent fresh-context harness):
`ChecklistItem.ParentId` is `Restrict` (self-reference) while
`WorkItemId` is `Cascade`, and SQLite's own DB-level cascade cannot
safely resolve deleting a self-referencing tree from a cold, untracked
`DbContext` — the legacy desktop app never hits this because its
`PlanningService` holds one long-lived context per session, so checklist
rows added earlier stay tracked and EF Core's own client-side cascade
(which orders self-referencing deletes correctly) substitutes for the
DB's raw cascade. This rebuild's `IDbContextFactory` gives every call a
fresh context (ADR-0002), so that masking never applies here. Fixed by
adding `.Include(w => w.Checklist)` before the `Remove` call — confirmed
this resolves it via a scratch console app seeding a full cascade
(nested checklist, note, status change, owner) against the live dev DB
and calling the real service methods; spec.md/design.md were updated to
document the required deviation from a literal legacy port. Logged as
`.specclaw/learnings.md` L28 (high priority) — flags that `BL-010`
(Project deletion) will need the identical treatment one level deeper.
T2 (coding agent) added the Delete button/dialog/callback and verified
the full fix end-to-end through the actual UI: created a task with the
exact nested-checklist/note/status-change/owner shape that used to crash,
clicked Delete through the browser, and confirmed via direct DB
inspection that every related row across all five tables was gone, with
the Accountability section updating with no manual refresh. Scope check:
`git diff` against pre-build master touched exactly the three declared
source files across both tasks, plus this change's own planning docs —
no deviation.

## Agent Runs

| Task | Agent | Model | Status | Duration |
|------|-------|-------|--------|----------|
| T1 | (applied directly, then fixed after live-verification caught a real bug — no subagent) | — | Complete | — |
| T2 | general-purpose coding agent | sonnet | Complete | ~37 min |

## Issues

1. **A build-verification agent (T1, initial attempt) discovered a
   genuine architectural gap, not just a code bug**: a literal verbatim
   port of the legacy `DeleteTaskAsync` is unsafe under this rebuild's
   `IDbContextFactory`-per-call pattern whenever a self-referencing
   relationship (currently only `ChecklistItem.ParentId`) is involved.
   Fixed in this change; `.specclaw/learnings.md` L28 flags the same risk
   for `BL-010`'s future design.
2. **A Visual Studio instance had this solution open**, locking build
   output and blocking an independent `dotnet build` re-check of T2's
   real working-tree changes (the coding agent had already verified a
   scratch mirror of the identical source built and ran cleanly). Asked
   the user to close/unload VS; re-ran the build against the real tree
   afterward and confirmed 0 errors before proceeding. Not a code issue —
   pure tooling contention.
