# Status: Project deletion (cascade)

**Change:** project-deletion
**Started:** 2026-08-03
**Last Updated:** 2026-08-03

## Progress

| Phase | Status | Notes |
|-------|--------|-------|
| Proposal | 🟢 Approved | Approved by proceeding to `/specclaw:plan` |
| Spec | 🟢 Complete | 5 FRs, 3 NFRs, 8 ACs, 3 edge cases |
| Design | 🟢 Complete | 1 new `PlanningService` method (with the confirmed-required `.Include` fix), `Projects.razor` gains a per-row Delete button + confirm dialog |
| Tasks | 🟢 Complete | 2 tasks / 2 waves |
| Build | 🟢 Complete | Both tasks done; merged to master |
| Verify | ❌ Failed | 5/8 acceptance criteria — AC3/AC4/AC6 fail live; see verify-report.md |

## Task Progress

**Completed:** 2 / 2
**Failed:** 0

Both tasks complete. `dotnet build` succeeds (0 errors). T1 (applied
directly) implemented `DeleteProjectAsync` exactly as already confirmed
during planning — `.Include(p => p.Tasks).ThenInclude(t => t.Checklist)`
before removing — and re-verified end-to-end against the real dev DB
with the full `GM-024` shape (objective, nested checklist, note, status
change, owner, meeting): zero rows remained across all eight affected
tables, the empty-project case (AC5) succeeded cleanly, and the
double-delete edge case was a silent no-op as expected. T2 (coding
agent, then independently re-verified) added the Delete button/dialog/
reload wiring; the agent's own environment lacked a connected browser
automation extension, so it verified AC2/AC5 via an independent
service-layer harness (again the full `GM-024` shape) and confirmed
AC1/AC7/AC8 by direct inspection, but could not click-test AC3/AC4/AC6.
The build orchestrator's environment also lacked a connected browser
extension, so a full interactive click-through wasn't possible there
either — but reasoning through the actual rendered HTML surfaced a real
gap: the initial `@onclick:stopPropagation`-only implementation would
not have reliably suppressed `MudListItem`'s native `<a href>` navigation
(`stopPropagation` blocks ancestor *listeners*, not a native element's
own default action — only `preventDefault` does). Fixed by adding
`@onclick:preventDefault="true"` alongside it; confirmed via the actual
server-rendered HTML that both `__internal_stopPropagation_onclick` and
`__internal_preventDefault_onclick` markers are now present. Logged as
`.specclaw/learnings.md` L29. AC3/AC4/AC6 rest on this code-level fix
plus DOM-inspection evidence, not an observed click — worth a live
spot-check next time browser automation tooling is available. Scope
check: `git diff` against pre-build master touched exactly the two
declared source files across both tasks, plus this change's own planning
docs — no deviation.

## Agent Runs

| Task | Agent | Model | Status | Duration |
|------|-------|-------|--------|----------|
| T1 | (applied directly — same fix already confirmed during planning) | — | Complete | — |
| T2 | general-purpose coding agent, then independently re-verified/fixed by the build orchestrator | sonnet | Complete | ~25 min (agent) |

## Issues

1. **Browser automation (`claude-in-chrome`) was unavailable in both the
   T2 coding agent's environment and the build orchestrator's own
   environment** — neither could click through the actual confirmation
   dialog/delete flow live. Verification for AC3/AC4/AC6 rests on code
   reading plus rendered-HTML inspection instead of an observed
   interaction. AC1/AC2/AC5/AC7/AC8 all have genuine live evidence
   (service-layer calls against the real dev DB, direct diff/grep
   inspection).
2. **A real bug was found and fixed during this process, not just a
   documentation gap**: `@onclick:stopPropagation` alone on the Delete
   icon button would not have reliably prevented `MudListItem`'s `Href`
   navigation from firing, because it renders as a native `<a>` tag whose
   default action requires `preventDefault`, not just stopped
   propagation. Fixed and confirmed via rendered HTML; logged as
   `.specclaw/learnings.md` L29 as a reusable pattern for any future
   "action button nested inside a navigable row" case.
