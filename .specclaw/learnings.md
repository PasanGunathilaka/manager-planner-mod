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

## [L2] agent_issue — specclaw-build-context produced a malformed payload for T...

**When:** 2026-07-27 06:13 UTC
**Category:** agent_issue
**Priority:** medium
**Status:** pending

### Detail
specclaw-build-context produced a malformed payload for T2: its '## Your Task' section concatenated T2's own Notes with T4's Notes text (PlanningDbContext/design-time-factory instructions bled into the entity-porting task). Separately, the payload's suggested commit message template mis-substituted config.yaml's commit_prefix, embedding the raw YAML line (including its inline comment) instead of the parsed value 'specclaw'. Both were caught before being handed to the coding agents -- hand-written prompts were used instead for T2/T3, and specclaw-build commit (not the payload's suggested message) was used for all commits.

### Action
Before trusting specclaw-build-context's generated payload verbatim for a multi-task tasks.md, spot-check the '## Your Task' and suggested commit-message sections for cross-task bleed and config-substitution bugs, especially when task Notes fields are long/multi-paragraph.

---

## [L3] design_gap — T1's declared file list (ManagerPlanner.sln, .gitignore, ...

**When:** 2026-07-27 06:13 UTC
**Category:** design_gap
**Priority:** low
**Status:** pending

### Detail
T1's declared file list (ManagerPlanner.sln, .gitignore, both .csproj files) undercounted what 'dotnet new blazor --interactivity Server' actually generates -- Program.cs, appsettings*.json, Components/*.razor, wwwroot/app.css, and Properties/launchSettings.json were all created by the same T1 scaffolding step but weren't in tasks.md's Files list. Committed anyway under a follow-up T1 commit since they're inherent template boilerplate, not scope creep.

### Action
When a task's Notes say to run a project-scaffolding template command (dotnet new ...), declare the files list as 'per the <template> template's default output' rather than enumerating a guessed subset, or expect a follow-up commit to catch the rest.

---

## [L4] agent_issue — specclaw-verify collect's changed-files evidence dump mis...

**When:** 2026-07-27 06:32 UTC
**Category:** agent_issue
**Priority:** medium
**Status:** pending

### Detail
specclaw-verify collect's changed-files evidence dump misreported 8 of 9 ported entity files (Project.cs, Objective.cs, WorkItem.cs, ChecklistItem.cs, TaskOwner.cs, Meeting.cs, ProgressNote.cs, StatusChange.cs, Enums.cs) as 'File does not exist', while correctly resolving the 9th (User.cs). All 9 files are present at src/ManagerPlanner.Core/Domain/*.cs and the build succeeds referencing every one of them. The verify agent caught this itself by independently reading the repo and re-running dotnet build, and did not let the tooling bug affect its verdict.

### Action
The evidence-extraction script behind specclaw-verify collect appears to resolve some changed-file paths as bare filenames instead of full repo-relative paths from tasks.md's Files: lists (a comma-separated multi-path line, e.g. T2's declares 'src/ManagerPlanner.Core/Domain/User.cs, Project.cs, Objective.cs, ...' with only the first entry fully qualified) -- fix the parser to carry the directory forward across comma-separated entries in a single Files: line, or always resolve against git ls-files.

---
