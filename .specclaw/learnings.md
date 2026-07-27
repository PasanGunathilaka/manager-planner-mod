# Learnings: scaffold-blazor-solution

Build learnings, spec gaps, and patterns discovered.

**Categories:** spec_gap | design_gap | pattern | best_practice | agent_issue

---

## [L1] design_gap — The proposal/design assumed 'no legacy source tree exists...

**When:** 2026-07-27 06:02 UTC
**Category:** design_gap
**Priority:** high
**Status:** pending

### Detail
The proposal/design assumed 'no legacy source tree exists in this repo to diff against directly' (used to justify fresh naming in design.md Key Decision 5), but the actual legacy repo is present on disk at C:\Learnings\Projects\manager-planner (sibling directory), with the real ExecutivePlanning.Core source. Ground-truthing T2's entity port against it caught real bugs the analysis-doc-only port missed: User.OwnedTasks was typed ICollection<WorkItem> instead of ICollection<TaskOwner> (would have broken T4's DbContext relationship mapping), and several entities were missing load-bearing default values (Project.CreatedUtc, WorkItem.CreatedUtc, StatusChange.ChangedUtc, ProgressNote.CreatedUtc/NoteDate, User.IsActive/Role, Meeting.Type/MeetingDate) that the legacy PlanningService relies on being set at the entity level rather than at call sites. PlanningRules validation also differed in real behavior, not just wording: legacy trims strings before the length check, and ValidateNoteDate uses DateTime.UtcNow (with an injectable nowUtc parameter for testability) rather than local DateTime.Today.

### Action
For every future backlog item (1-13), ground-truth against the real legacy source at C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core first, not just the .specclaw/analysis/*.md summaries -- the docs are a good index but the source is the actual golden master ADR-0005 calls for. Update design.md's Key Decision 5 rationale (the 'no legacy tree' premise) and consider revisiting rebuild-backlog.md's per-item 'verification inputs needed' notes that assumed doc-only availability.

---
