# Status: Task status transitions and the StatusChange audit trail

**Change:** task-status-transitions
**Started:** 2026-07-28
**Last Updated:** 2026-07-28

## Progress

| Phase | Status | Notes |
|-------|--------|-------|
| Proposal | 🟢 Approved | Approved by proceeding to `/specclaw:plan` |
| Spec | 🟢 Complete | 5 FRs, 3 NFRs, 9 ACs |
| Design | 🟢 Complete | 1 new PlanningService method; extends TaskRow + ProjectDetail |
| Tasks | 🟢 Complete | 2 tasks / 2 waves |
| Build | 🟢 Complete | Both tasks done; merged to master |
| Verify | ✅ Passed | Run `/specclaw:verify` next |

## Task Progress

**Completed:** 2 / 2
**Failed:** 0

All tasks complete. `dotnet build` succeeds (0 errors). AC1-AC9 verified
end-to-end through the running app + direct DB inspection: clicked "Mark
done" on a task (Done: 1, CompletedUtc set, summary auto-updated with no
manual refresh — AC2/AC3/AC6), then "Blocked" on the same task
(CompletedUtc cleared back to null — AC3), then re-clicked "Blocked" again
— DB inspection confirmed exactly 2 `StatusChange` rows total (not 3),
proving the no-op guard works (AC4). Confirmed all four buttons render and
work independently on both the per-objective and Ungrouped rows (AC5),
`Reason` stays null on every row (AC7), and exactly 10 `PlanningService`
methods exist (AC8). Real mouse-click dispatch was wedged wholesale again
— a 4th change running into this (see learnings L7/L8/L12/L15) — worked
around via in-page JS dispatch as usual.

`specclaw-build finalize` failed on its first attempt (uncommitted
`tasks.md` checkbox updates blocked the branch checkout for merge — see
learning L16); fixed by committing them and re-running finalize, which
then succeeded.

## PR

No separate GitHub PR — same `git.strategy: branch-per-change` constraint
as the three prior changes: `specclaw-build finalize` had already merged
`specclaw/task-status-transitions` into `master` locally, so `specclaw-pr`
found head == base. `master` was already up to date on `origin` (pushed
automatically during the verify/build steps):
https://github.com/PasanGunathilaka/manager-planner-mod

## Agent Runs

| Task | Agent | Model | Status | Duration |
|------|-------|-------|--------|----------|

## Issues

_None._
