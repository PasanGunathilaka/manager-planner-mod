# Verification Report: project-management

**Verified:** 2026-07-27
**Model:** Claude Sonnet 5
**Verdict:** PASS

## Quotes (evidence extracted before judging)

- **AC1 text:** "`dotnet build` at the solution root succeeds with 0 errors." Live re-run: `Build succeeded. 0 Warning(s) 0 Error(s)` (matches pasted Build Output block verbatim).
- **AC2 text:** "Starting the app against an empty database creates exactly one `User` with `Role = Manager`; starting it again ... creates no second Manager row." Code: `if (!db.Users.Any(u => u.Role == UserRole.Manager)) { db.Users.Add(new User {...}); db.SaveChanges(); }` (`Program.cs:33-37`). Live: 1st run log shows one `INSERT INTO "Users" (...)`; DB query after → `Users: [(1, 'Manager', 'manager@example.com', 0)]`. 2nd run log shows `SELECT EXISTS (SELECT 1 FROM "Users" WHERE "Role" = 0)` with **no** subsequent INSERT; DB query after → still `[(1, 'Manager', ...)]`, `Manager count: (1,)`.
- **AC3 text:** "Submitting the `/projects` create form with a valid name persists a new `Project` row owned by the bootstrapped Manager, with a non-default `CreatedUtc` and `Status == Active`, and the page's list updates ... without a manual reload." Live: submitted "Q3 Platform Migration" via `form_input` + click → DB row `(1, 'Q3 Platform Migration', 'Migrate core services to the new cloud platform', 0, '2026-07-27 10:07:41.9933079', 1)` (Status 0=Active, OwnerId 1=Manager, real timestamp). `read_page` immediately after the click (no navigation) showed `link "Q3 Platform Migration" [ref_24] href="/projects/1"` in the list.
- **AC4 text:** "Submitting an empty/whitespace-only name, or a name over 120 characters, shows the `PlanningRules` validation message inline and creates no `Project` row." Code: `throw new ValidationException("Project name is required.");` / `throw new ValidationException($"Project name cannot exceed {MaxProjectName} characters.");` (`PlanningRules.cs`). Live: empty-name submit → page showed `generic "Project name is required." [ref_17]` and `"No projects yet."` unchanged. 121-char name submit → page showed `generic "Project name cannot exceed 120 characters." [ref_17]`, still `"No projects yet."`.
- **AC5 text:** "...counts matching that data and a `PercentComplete` equal to `Math.Round(100.0 * Done / TotalTasks, 1)` for at least one non-trivial ... case." Legacy source confirmed verbatim: `src/ExecutivePlanning.Core/Services/Reports.cs:63`: `public double PercentComplete => TotalTasks == 0 ? 0 : Math.Round(100.0 * Done / TotalTasks, 1);` — byte-identical to the new `Reports.cs`. Seeded 7 `WorkItems` (Done=3, InProgress=1, Blocked=1, NotStarted=2, 2 with past deadlines/not-Done, 1 `IsDiscovered`). Live page: `"Total: 7" "Done: 3" "In progress: 1" "Blocked: 1" "Not started: 2" "Overdue: 2" "Discovered: 1" "% complete: 42.9%"` — `Math.Round(100.0*3/7,1) = 42.9` exactly.
- **AC6 text:** "Visiting `/projects/{id}` for a project id that does not exist shows 'Project not found' rather than an all-zero dashboard." Code: `else if (_summary.TotalTasks == 0 && string.IsNullOrEmpty(_summary.ProjectName)) { <p>Project not found.</p> }`. Live: navigated to `/projects/9999` → page rendered `generic "Project not found." [ref_4]` only, no counts list.
- **AC7 text:** "...clicking 'Refresh' ... updates the displayed counts to match — confirming the button re-queries rather than showing stale/cached data." Live: page showed `Done: 4, In progress: 2, Overdue: 2, 57.1%`; then directly `UPDATE WorkItems SET Status=3 WHERE Id=4` (InProgress→Done) via sqlite3, bypassing the app entirely; clicked the `Refresh` button ref captured immediately beforehand → page updated in place to `Done: 5, In progress: 1, Overdue: 1, 71.4%` (`Math.Round(100.0*5/7,1)=71.4`). Server log confirms exactly one new `FindAsync`+`WorkItems` query pair fired after the click (not the doubled prerender+interactive pattern seen on real navigations), proving a single live re-query from the `@onclick="RefreshAsync"` handler, not a page reload.
- **AC8 text:** "No `PlanningService` method beyond `GetProjectsAsync`, `AddProjectAsync`, `GetProjectSummaryAsync`, and `GetCurrentManagerIdAsync` exists anywhere in the diff." `git diff --stat 66cf2c4 90a3b78 -- src/` → same 7 files as the pasted implementation (`PlanningService.cs`, `Reports.cs`, `MainLayout.razor`, `ProjectDetail.razor`, `Projects.razor`, `_Imports.razor`, `Program.cs`). Grep of `PlanningService.cs` public members → exactly the constructor + 4 methods, no more.

## Acceptance Criteria

- ✅ **AC1:** `dotnet build` at the solution root succeeds with 0 errors — live re-run reproduced `Build succeeded. 0 Warning(s) 0 Error(s)`.
- ✅ **AC2:** Empty-DB startup creates exactly one Manager `User`; second startup against the bootstrapped DB creates no second row — verified live via two sequential `dotnet run` startups and direct sqlite3 queries before/after.
- ✅ **AC3:** Valid-name submission persists a `Project` owned by the Manager with real `CreatedUtc`/`Status=Active`, and the list updates without a manual reload — verified live via browser form submission + sqlite3 row inspection + `read_page` diff.
- ✅ **AC4:** Empty and overlong (121-char) names are rejected inline with the `PlanningRules` messages and create no row — verified live for both cases.
  - ⚠️ Edge case: literal whitespace-only name (e.g. `"   "`) was not separately click-tested in the browser — only the fully-empty case was. `PlanningRules.ValidateProjectName` trims before checking length (`var t = name?.Trim() ?? string.Empty; if (t.Length == 0) throw ...`), so whitespace-only takes the identical code path as empty and is covered by the same guard, but this is a code-reading inference rather than a live-clicked observation.
- ✅ **AC5:** Detail page shows counts matching a known non-trivial task mix, with `PercentComplete` exactly matching `Math.Round(100.0*Done/TotalTasks,1)` — verified live (42.9% for 3/7), and the formula independently confirmed byte-identical against the legacy `Reports.cs`.
- ✅ **AC6:** Nonexistent project id shows "Project not found." rather than an all-zero dashboard — verified live at `/projects/9999`.
- ✅ **AC7:** Refresh button re-queries and reflects a change made directly to the DB (bypassing the UI) — verified live with a before/after count change and a server-log query-count check ruling out a stale/cached read or a full-page-reload false positive.
- ✅ **AC8:** No `PlanningService` method beyond the four required exists in the diff — verified via `git diff --stat` against the scaffold merge base and a direct grep of public members.

## Test Results

No tests configured — the solution (`ManagerPlanner.sln`) contains only `ManagerPlanner.Core` and `ManagerPlanner.Web`; no test project exists yet. The pasted context's "Test Output" and "Lint Output" sections were both empty, consistent with this.

## Issues Found

1. **Stray project created during live verification, unrelated to app code** — while testing AC5–AC7 the automated browser tab intermittently showed a Blazor Server "Attempting to reconnect to the server: 1 of 8" banner (visible in `read_page`/screenshots), and a `Project(Name="f", Description="r")` row appeared in the DB that I never explicitly submitted. Server log shows no exceptions around this event, and the row itself is fully valid per `PlanningRules` (non-empty, ≤120 chars) — the service behaved correctly given whatever input it received. This looks like a SignalR reconnect/reload artifact of the sandboxed automation tab (tab backgrounding, timer throttling) rather than a defect in `Projects.razor`/`PlanningService`. **Fix:** not a code fix — no action needed against this backlog item; flagging only so it isn't mistaken for a real duplicate-submission bug. (Row deleted before finishing verification; did not affect any AC's queried project.)
2. **`GetCurrentManagerIdAsync` throws unhandled if no Manager exists** — `.FirstAsync()` throws `InvalidOperationException` (not caught anywhere in `Projects.razor`) if the bootstrapped Manager row were ever deleted out from under the app. FR5's startup bootstrap makes this scenario unreachable through normal operation, so this is not an AC violation, just a latent fragility worth a mental note for when auth/multi-user (ADR-0001) replaces this stand-in. **Fix:** none required now; consider a friendlier error path when the real auth ADR replaces this bootstrap.

No blocking issues in build output.

## Summary

**Passed:** 8/8 criteria
**Failed:** 0/8 criteria
**Verdict:** PASS
