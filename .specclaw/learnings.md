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

## [L5] agent_issue — specclaw-pr's first-run test-policy prompt (check_test_po...

**When:** 2026-07-27 06:46 UTC
**Category:** agent_issue
**Priority:** high
**Status:** pending

### Detail
specclaw-pr's first-run test-policy prompt (check_test_policy in bin/specclaw-pr) does 'read -r policy </dev/tty' in a retry loop with no non-interactive fallback. In this non-interactive shell environment /dev/tty doesn't exist, so the read fails every iteration and the while-true loop spins forever printing '/dev/tty: No such device or address', never timing out. Had to kill the process manually (via PowerShell Stop-Process) and pre-set pr.test_policy in config.yaml to skip the prompt on retry. Separately, /specclaw:pr also assumes an unmerged feature branch exists to PR from, but git.strategy: branch-per-change's specclaw-build finalize step already merges the branch into the base branch locally as its designed behavior -- so by the time /specclaw:pr runs, head branch == base branch and 'gh pr create' correctly refuses ('head branch is the same as base branch'). Resolved by pushing master directly instead of opening a PR (user's explicit choice over rewinding local history to fabricate a diff).

### Action
(1) specclaw-pr should detect a missing/non-functional /dev/tty (e.g. check  or  first) and fail fast with an actionable error instead of spinning, or accept the policy via a CLI flag/env var for non-interactive runs. (2) For git.strategy: branch-per-change projects that also want GitHub PR review, specclaw-build's finalize step and /specclaw:pr are currently incompatible -- finalize should not auto-merge to base when a PR is the intended next step (or config needs a third strategy like 'branch-per-change-pr' that pushes instead of merging). Flag this to a human before it recurs on backlog item 1's PR.

---

## [L6] agent_issue — Browser-based UI verification (claude-in-chrome) of the B...

**When:** 2026-07-27 09:54 UTC
**Category:** agent_issue
**Priority:** medium
**Status:** pending

### Detail
Browser-based UI verification (claude-in-chrome) of the Blazor Server InteractiveServer pages produced two categories of false signals during T5's manual verification: (1) the computer.type action (simulated keystrokes) corrupted InputText field values under Blazor Server's round-trip binding -- typing 'Q3 Platform Migration' resulted in a persisted Project row with Name='tt', and a duplicate/extra row was created, suggesting dropped/raced keystroke events against the SignalR circuit. Switching to the form_input tool (which sets the DOM value directly in one operation) fixed this immediately and reliably. (2) Element refs returned by read_page went stale across Blazor re-renders -- clicking a previously-obtained 'Refresh' button ref sometimes silently did nothing (or, once, produced a 'no element found' error after the page had re-rendered), making the Refresh button appear broken. A clean re-test (read_page immediately before the click, with zero intervening tool calls) proved the Refresh button and its underlying GetProjectSummaryAsync re-query work correctly -- confirmed by server-log query-count diffing (exact line-count deltas around the click) and the resulting on-page counts changing to match a direct DB edit made between load and refresh.

### Action
For future Blazor Server (or any SignalR-interactive) UI verification via claude-in-chrome: (a) always use form_input for text/textarea fields, never computer.type; (b) always call read_page immediately before a click with no intervening tool calls, since refs can go stale after any re-render; (c) when a click appears to have no effect, verify via server-log query-count diffing (count matching log lines before/after) before concluding the feature is broken -- this distinguishes 'the click never reached the handler' from 'the handler ran but rendering didn't visibly update', which are very different bugs.

---

## [L7] agent_issue — Blazor build failed with a file-lock error (MSB3027/MSB30...

**When:** 2026-07-27 12:22 UTC
**Category:** agent_issue
**Priority:** medium
**Status:** pending

### Detail
Blazor build failed with a file-lock error (MSB3027/MSB3021) because Visual Studio 2022 had ManagerPlanner.Web.exe running/debugging, holding the DLL open -- not a leftover process from a prior specclaw session this time. Confirmed via 'Microsoft Visual Studio 2022 (PID), ManagerPlanner.Web (PID)' in the lock error message before killing anything. Asked the user for confirmation before stopping the process since it looked like their own active work, not build-tooling cleanup.

### Action
When a dotnet build file-lock error names 'Microsoft Visual Studio' as the lock holder (not a bare dotnet.exe/ManagerPlanner.Web.exe from an earlier specclaw run), treat it as the user's own active session and confirm before killing the process -- don't assume every lock is a leftover artifact safe to clear unilaterally.

---

## [L8] agent_issue — claude-in-chrome click dispatch failed wholesale during T...

**When:** 2026-07-27 12:23 UTC
**Category:** agent_issue
**Priority:** medium
**Status:** pending

### Detail
claude-in-chrome click dispatch failed wholesale during T2 verification -- not just stale refs (the item-1 pattern already documented), but a case where EVEN a plain <a href> link click and a previously-working 'Refresh' button both silently did nothing, on both the original tab and a freshly closed-and-recreated tab, with the WebSocket circuit connected and no console/server errors. A computer.action:'screenshot' call had also timed out earlier with 'renderer may be frozen or unresponsive', suggesting the underlying Chrome renderer process itself was wedged, not just Blazor-specific state. Recovered by falling back to a small scratch console app (referencing ManagerPlanner.Core directly, using a minimal IDbContextFactory wrapper around the same SQLite file) that called the real PlanningService.AddObjectiveAsync/GetPlannerForProjectAsync methods in-process -- this verified the actual business logic (AC2/AC3/AC4) without depending on the browser at all, then get_page_text (which kept working throughout, unlike clicks) confirmed the rendering (AC5/AC6/AC8) after reloading the page.

### Action
When claude-in-chrome clicks stop working across ALL elements (not just one ref) including plain navigation links, and closing/recreating the tab doesn't fix it, don't keep retrying clicks -- the renderer itself may be wedged. Fall back to: (1) get_page_text/read_page for static rendering checks (these kept working), and (2) a small scratch console app referencing the app's own library project to call service methods directly in-process for anything requiring an actual state-changing action -- this is a reliable, fast way to verify business logic when browser interactivity is unavailable, and works for any .NET project with a testable service layer.

---

## [L9] pattern — GetPlannerForProjectAsync's multi-collection Include chai...

**When:** 2026-07-27 12:57 UTC
**Category:** pattern
**Priority:** low
**Status:** pending

### Detail
GetPlannerForProjectAsync's multi-collection Include chain (Tasks->Owners, Tasks->Checklist) already triggers EF Core's 'Compiling a query which loads related collections for more than one collection navigation... no QuerySplittingBehavior configured' warning at runtime, even though Tasks is currently always empty. Harmless today, but this is the exact query item 3 (Task/WorkItem) will start populating with real rows.

### Action
When item 3 (or later) starts returning non-trivial Tasks/Owners/Checklist counts through this same GetPlannerForProjectAsync query, add .AsSplitQuery() to avoid a cartesian-product join blowing up row counts -- flag this during that item's design phase rather than waiting for a performance complaint.

---

## [L10] spec_gap — The proposal/spec's initial description-trimming decision...

**When:** 2026-07-28 04:35 UTC
**Category:** spec_gap
**Priority:** medium
**Status:** pending

### Detail
The proposal/spec's initial description-trimming decision ("Description stored exactly as passed, not trimmed") was grounded only in the legacy AddTaskAsync service method. Re-reading the actual caller, MainWindowViewModel.AddTaskAsync, during build showed it pre-trims/nulls the description before calling the service (../manager-planner/src/ExecutivePlanning.Desktop/ViewModels/MainWindowViewModel.cs:146). Caught before implementation via re-verification, but the initial spec/design would have shipped a real fidelity miss.

### Action
When grounding a ported method's fidelity against legacy source, trace the full caller-to-service call chain, not just the method signature being ported -- pre-processing done by the caller is part of the app's real behavior.

---

## [L11] best_practice — Naming a Blazor component parameter 'Task' (as originally...

**When:** 2026-07-28 04:35 UTC
**Category:** best_practice
**Priority:** low
**Status:** pending

### Detail
Naming a Blazor component parameter 'Task' (as originally planned in tasks.md for TaskRow.razor's WorkItem parameter) risks colliding with System.Threading.Tasks.Task once ImplicitUsings is enabled (it is, on ManagerPlanner.Web) -- any async method in the same component referencing the bare Task type, or calling Task.CompletedTask/Task.Delay, would resolve to the property instead. Renamed the parameter to 'WorkItem' (matching the entity type name) instead.

### Action
Avoid naming Razor/Blazor component parameters after common BCL type names (Task, Type, Object, etc.) even when the parameter refers to a same-shaped domain entity -- prefer the domain type's own name.

---

## [L12] pattern — Real mouse-click dispatch via claude-in-chrome's computer...

**When:** 2026-07-28 04:35 UTC
**Category:** pattern
**Priority:** high
**Status:** pending

### Detail
Real mouse-click dispatch via claude-in-chrome's computer tool was wedged wholesale during verification -- even plain <a href> link clicks did nothing (no navigation, no server query in the log). This is the third occurrence of this exact failure mode noted against this project (previously logged as L7/L8 during planner-grid's verification). In-page JS dispatch (element.click() via javascript_tool) worked immediately as a substitute for every click in this session.

### Action
Recurrence >= 3 for the same failure mode across changes in this project -- consider defaulting to JS-dispatched clicks (or trying them first) for claude-in-chrome verification on this project rather than mouse-simulated clicks, to skip the diagnose-then-fallback step each time.

---

## [L13] agent_issue — A Visual Studio debug session (ManagerPlanner.Web.exe) wa...

**When:** 2026-07-28 04:35 UTC
**Category:** agent_issue
**Priority:** low
**Status:** pending

### Detail
A Visual Studio debug session (ManagerPlanner.Web.exe) was running at the start of this build, locking the Web project's build output and causing 'dotnet build' on the full solution to fail with MSB3027 file-lock errors, even though the code itself compiled cleanly (verified by building ManagerPlanner.Core alone). Resolved by asking the user for permission, then stopping the process.

### Action
Before running a full-solution 'dotnet build' during /specclaw:build, check for a running ManagerPlanner.Web.exe (or the equivalent process for other projects) that could lock output files, since this project is being actively debugged in Visual Studio during development.

---

## [L14] pattern — tasks.md's line-wrapping broke specclaw-parse-tasks a sec...

**When:** 2026-07-28 06:04 UTC
**Category:** pattern
**Priority:** medium
**Status:** pending

### Detail
tasks.md's line-wrapping broke specclaw-parse-tasks a second time (task-management's T2 hit this first, now fixed there): T2's title AND its Files: line both wrapped across two lines here, silently dropping files/depends/estimate from the parsed task until fixed. Caught before build by re-running specclaw-parse-tasks and checking the output looked complete, not by any tool warning.

### Action
When authoring tasks.md during /specclaw:plan, always keep each task's title line and Files: line as a single unwrapped line, regardless of length -- verify with specclaw-parse-tasks immediately after writing tasks.md, before starting build, and treat an empty files/depends/estimate field in the parsed output as a red flag to inspect the raw markdown.

---

## [L15] pattern — Real mouse-click dispatch via claude-in-chrome was wedged...

**When:** 2026-07-28 06:04 UTC
**Category:** pattern
**Priority:** high
**Status:** pending

### Detail
Real mouse-click dispatch via claude-in-chrome was wedged wholesale during verification for a FOURTH change running (planner-grid, project-management, task-management, now task-status-transitions) -- a real click on a status button produced zero server-side query activity, while in-page JS dispatch (element.click()) worked immediately every time.

### Action
Recurrence >= 3 was already flagged in task-management (L12); now at 4. Strongly consider defaulting straight to JS-dispatched clicks for any claude-in-chrome browser verification on this specific project/environment, skipping the real-click attempt entirely, rather than re-diagnosing the same wedge each time.

---

## [L16] design_gap — specclaw-build finalize failed on the first attempt: 'Fai...

**When:** 2026-07-28 06:04 UTC
**Category:** design_gap
**Priority:** high
**Status:** pending

### Detail
specclaw-build finalize failed on the first attempt: 'Failed to checkout master for merge... Your local changes to the following files would be overwritten... .specclaw/changes/task-status-transitions/tasks.md'. Root cause: specclaw-update-task-status mutates tasks.md's checkbox markers directly, but specclaw-build commit only stages the task's own declared source files (as passed on its command line), not tasks.md itself -- so every task's [ ] -> [x] edit is left uncommitted on the feature branch until something else happens to commit it. finalize's branch-per-change merge requires a clean working tree to check out master, so it aborted.

### Action
Before calling specclaw-build finalize, always run git status and commit any pending tasks.md/STATUS.md changes first (or fold the tasks.md checkbox update into each task's own specclaw-build commit call by passing tasks.md as an extra file). Don't assume finalize will succeed on the first try without checking working-tree cleanliness beforehand.

---

## [L17] best_practice — Spawned 3 independent coding agents in parallel for Wave ...

**When:** 2026-07-28 07:24 UTC
**Category:** best_practice
**Priority:** medium
**Status:** pending

### Detail
Spawned 3 independent coding agents in parallel for Wave 2 (T2: Home/Error, T3: Projects, T4: ProjectDetail/TaskRow) since they touched completely disjoint files with no worktree isolation needed. All three succeeded first-try with 0 build errors, including T4 which was the largest/most complex task in this change (MudSelect<int?> binding conversion, MudSimpleTable wrapping, MudDatePicker, MudButtonGroup).

### Action
For a multi-file restyle/refactor change where sub-tasks touch disjoint files, default to spawning them in parallel within a wave rather than sequentially -- git working-tree isolation isn't needed when files don't overlap, and it cuts wall-clock time significantly (3 tasks in ~1 sequential-task's worth of wait).

---

## [L18] best_practice — specclaw-build finalize succeeded on the FIRST attempt th...

**When:** 2026-07-28 07:24 UTC
**Category:** best_practice
**Priority:** medium
**Status:** pending

### Detail
specclaw-build finalize succeeded on the FIRST attempt this time, unlike task-status-transitions where it failed on uncommitted tasks.md checkboxes (L16). This time git status was checked and tasks.md/STATUS.md were committed proactively before calling finalize, exactly as L16 recommended.

### Action
The L16 mitigation (always git status + commit tasks.md/STATUS.md immediately before specclaw-build finalize) works -- keep doing this every time, it turned a previously-reliable failure point into a non-issue.

---

## [L19] best_practice — During T5's final re-verification, skipped the real-mouse...

**When:** 2026-07-28 07:24 UTC
**Category:** best_practice
**Priority:** low
**Status:** pending

### Detail
During T5's final re-verification, skipped the real-mouse-click attempt entirely and went straight to JS dispatch (element.click()) for every interaction, per L15's recommendation after 4 prior changes hit the same wedged-renderer issue. This saved the diagnose-then-fallback round trip and worked flawlessly for every form submission and status-button click tested.

### Action
Continue defaulting straight to JS-dispatched clicks for claude-in-chrome verification on this project -- L15's recommendation is confirmed sound, no need to re-attempt real clicks first each time.

---

## [L20] best_practice — T4's coding agent (the largest task: ProjectDetail.razor ...

**When:** 2026-07-28 07:24 UTC
**Category:** best_practice
**Priority:** medium
**Status:** pending

### Detail
T4's coding agent (the largest task: ProjectDetail.razor + TaskRow.razor, converting to MudSelect<int?>/MudDatePicker/MudSimpleTable/MudChip/MudButtonGroup) wrote a small throwaway reflection console app against the actual installed MudBlazor.dll (9.7.0) to confirm exact property names (MudDatePicker.Date, MudSimpleTable.Hover/Dense, MudChip<T>.Color, etc.) before writing any markup, instead of guessing from training-data API knowledge. Result: zero MudBlazor API-mismatch compile errors on the first build attempt for the most complex file in the change.

### Action
When a coding agent (or Claude directly) is about to write markup/code against a third-party library API it isn't 100% certain of, encourage verifying exact member names against the actually-installed package (via reflection, decompilation, or reading the package's own source/docs) before writing code, rather than writing-then-fixing compile errors reactively -- this is cheap insurance that fully paid off here.

---

## [L21] agent_issue — specclaw-build-context's awk extraction for a task's Note...

**When:** 2026-07-31 06:36 UTC
**Category:** agent_issue
**Priority:** medium
**Status:** pending

### Detail
specclaw-build-context's awk extraction for a task's Notes field only captures the single line immediately after '- Notes:' and does not continue into wrapped continuation lines (unlike specclaw-parse-tasks, which does capture the full multi-line Notes text). For T1 this silently truncated a detailed, multi-paragraph grounding note (exact legacy file:line citations, exact exception message, exact field logic) down to one sentence, and the truncation logic also leaked wave 2's (T2's) first Notes line into T1's rendered 'Your Task' section. Worked around this build by supplying the full, correct Notes text directly from tasks.md myself when doing/delegating T1 and T2, rather than relying solely on the generated context payload.

### Action
Fix specclaw-build-context's Notes-field awk block to capture all indented continuation lines following '- Notes:' (stopping at the next '  - ' bullet or a blank/dedented line), matching specclaw-parse-tasks's own multi-line handling — otherwise every coding agent for a task with a long Notes field silently loses most of its grounding detail.

---

## [L22] agent_issue — specclaw-verify update-status's row-replace logic matches...

**When:** 2026-07-31 07:28 UTC
**Category:** agent_issue
**Priority:** low
**Status:** pending

### Detail
specclaw-verify update-status's row-replace logic matches any markdown table line starting with '| Verify' rather than scoping to the Progress table specifically. This change's status.md has a second table (Agent Runs) with its own '| Verify | <agent> | <model> | <verdict> | <duration> |' row; running 'specclaw-verify update-status' clobbered that row down to '| Verify | ✅ Passed | sonnet |', losing the agent/duration columns. Fixed manually after noticing it in the file-changed system reminder.

### Action
Scope specclaw-verify update-status's row match to the '## Progress' table section (e.g. stop matching once a later '## ' heading is reached, or anchor on the full '| Verify | <status-emoji> Pending |' pattern instead of a bare '| Verify' prefix) so it can't collide with an unrelated table using the same row label.

---
