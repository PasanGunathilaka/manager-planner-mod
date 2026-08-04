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
| Verify | ❌ Failed (round 1) | 5/8 acceptance criteria — AC3/AC4/AC6 fail live; see verify-report.md |
| Remediation | 🟢 Complete | Sibling-restructuring (per user's explicit direction) + `@key` fix; re-verified live, see below |

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
   dialog/delete flow live. Verification for AC3/AC4/AC6 rested on code
   reading plus rendered-HTML inspection instead of an observed
   interaction going into `/specclaw:verify`. AC1/AC2/AC5/AC7/AC8 all had
   genuine live evidence (service-layer calls against the real dev DB,
   direct diff/grep inspection).
2. **`/specclaw:verify` (round 1) found the stopPropagation+preventDefault
   fix insufficient in practice**: 7 of 9 live clicks on the Delete icon
   navigated away instead of showing the dialog, because Blazor Web App's
   document-level enhanced navigation intercepts anchor clicks
   independently of a component's own `@onclick` modifiers, even when the
   button is wrapped with both `stopPropagation` and `preventDefault`.
   Verdict: FAIL, 5/8 ACs (see `verify-report.md`).
3. **Remediated per the user's explicit choice ("Restructure as sibling")**:
   removed `Href` from `MudListItem`; the Delete button is now a true DOM
   sibling of a plain `<a>` (no ancestor-anchor relationship left to race
   against). Re-verified with genuine live browser clicks (this session,
   with `claude-in-chrome` available): across a careful, paced second
   round of testing, the dialog reliably appeared, Cancel correctly
   aborted, a confirmed Delete correctly removed only the intended row
   and reloaded the list automatically, and normal row-name navigation
   kept working. Final DB state cross-checked directly against the
   dev SQLite file and matches the UI exactly.
4. **A project ("ggg") was found deleted with no confirming click observed
   in between two consecutive tool calls**, during this same live-testing
   round, before the `@key` fix below was applied. Confirmed via direct
   DB inspection (not a rendering glitch — the row was genuinely gone).
   Traced to the `@foreach` over `_projects` having no `@key`; this list
   reorders on every add (`OrderByDescending(CreatedUtc)`), and Blazor's
   positional DOM-node reuse can misattribute a click/element across a
   re-render for reorderable lists without a key. Fixed by adding
   `@key="project.Id"` to `MudListItem`. No further unconfirmed or
   wrong-row deletions occurred in subsequent testing. "ggg" was an
   obviously-placeholder test project (matching a pattern of scratch data
   left by prior build/verify sessions in this same dev DB), not real
   user data.
5. **A residual, low-frequency (~1 in 5) "click produces no visible
   reaction at all" case remains** even after both fixes — every such
   case observed was a clean no-op (no dialog, no navigation, no
   deletion), and a deliberate retry always then succeeded. This most
   likely reflects click-dispatch latency in the CDP-based browser
   automation tooling itself (which independently showed unrelated
   flakiness this session: stale tab console state, viewport-size
   fluctuation across `read_page` calls, a tab group vanishing after
   closing one tab) rather than an application defect, but this was not
   root-caused with full certainty and is worth a spot-check if it
   recurs in a future change.
6. Two real bugs were found and fixed during this process, not just
   documentation gaps: (a) `stopPropagation`+`preventDefault` together
   were still insufficient against enhanced navigation for a
   nested-in-anchor button — logged as `.specclaw/learnings.md` L29
   (superseded) / L30 (new, corrected pattern); (b) the missing `@key`
   on a reorderable `@foreach` — logged as `.specclaw/learnings.md` L30.
