# Verification Report: accountability-reporting

**Verified:** 2026-08-03
**Model:** Claude Sonnet 5
**Verdict:** PASS

> **Methodology note:** the verify-agent payload's embedded excerpts of
> `PlanningService.cs` (truncated after `GetMeetingsForProjectAsync`, before
> `GetAccountabilityReportAsync`/`GetAccountabilityForAllProjectsAsync` —
> the FR2/FR3 methods AC2–AC13 hinge on) and `ProjectDetail.razor`
> (truncated mid-Meetings-section, before the Accountability section /
> `@code` block AC14/AC7/AC8 hinge on) did not contain the code most of
> these criteria depend on. This review reads the real files directly from
> `C:\Learnings\Projects\manager-planner-mod\src\...` instead of trusting
> the truncated excerpts, and reads all 11 fixtures
> (`.specclaw/baseline/fixtures/GM-008.json`…`GM-018.json`) plus their
> `scenarios.md` entries directly rather than deferring to the prior
> build-time claim that they passed.

## Acceptance Criteria

- ✅ **AC1** — `dotnet build` at the solution root succeeds with 0 errors — payload's Build Output: `"Build succeeded. 0 Warning(s) 0 Error(s)"`; independently re-run in this session with the same result (`ManagerPlanner.Core -> ...dll`, `ManagerPlanner.Web -> ...dll`, `Build succeeded. 0 Warning(s) 0 Error(s)`).

- ✅ **AC2** — `PromiseKept=true,PromiseBroken=true,IsOverdue=true` ⇒ `"Kept promise"` — `Reports.cs`'s `Verdict` getter: `if (PromiseKept) return "Kept promise";` is the first branch, so it fires before either losing flag is ever inspected. `GM-008.json` input `{"PromiseKept": true, "PromiseBroken": true, "IsOverdue": true, ...}` → output `{"verdict": "Kept promise"}` — matches exactly.

- ✅ **AC3** — `PromiseKept=false,PromiseBroken=true,IsOverdue=true` ⇒ `"BROKE promise"` — `if (PromiseBroken) return "BROKE promise";` is the second branch. `GM-009.json` input matches this shape, output `{"verdict": "BROKE promise"}` — matches.

- ✅ **AC4** — `PromiseKept=false,PromiseBroken=false,IsOverdue=true,LatestPromisedDate=<future>` ⇒ `"Overdue (no promise)"`, the CQ-019 quirk — `Reports.cs` line order is `if (PromiseKept)… if (PromiseBroken)… if (IsOverdue) return "Overdue (no promise)";` **then** `if (LatestPromisedDate.HasValue) return "Promise pending";`. `IsOverdue` is checked and returns before `LatestPromisedDate.HasValue` is ever reached. `GM-010.json` input `{"PromiseKept": false, "PromiseBroken": false, "IsOverdue": true, "latestPromisedDate": "2099-01-01T00:00:00"}` (a real, non-null future date) → output `{"verdict": "Overdue (no promise)"}`. Traced by hand: check1 false, check2 false, check3 true → returns immediately, `LatestPromisedDate.HasValue` (true) is never consulted. **The quirk is preserved exactly as required by FR4** — this is not silently "fixed."

- ✅ **AC5** — `..., IsOverdue=false, LatestPromisedDate=<future>` ⇒ `"Promise pending"` — falls through checks 1–3, hits `if (LatestPromisedDate.HasValue) return "Promise pending";`. `GM-011.json` matches this shape and output `{"verdict": "Promise pending"}`.

- ✅ **AC6** — all flags/`LatestPromisedDate` falsy/null ⇒ `"On track"` — falls through every check to `return "On track";`. `GM-012.json` input all false/null → output `{"verdict": "On track"}`.

- ✅ **AC7** — `Done` task, `CompletedUtc.Date <= PromisedDate.Date` ⇒ `PromiseKept=true, PromiseBroken=false, Verdict="Kept promise"` — `PlanningService.cs:299-303`: `if (t.Status == WorkItemStatus.Done) { row.PromiseKept = t.CompletedUtc.HasValue && t.CompletedUtc.Value.Date <= promised.Date; row.PromiseBroken = !row.PromiseKept; }` — confirms the `<=`, not `<`. `GM-013.json`: `promisedDate: "2026-07-30..."`, `completedUtc: "2026-07-30T09:05:37..."` (same calendar date) → output `{"PromiseKept": true, "PromiseBroken": false, "Verdict": "Kept promise"}` — matches the exact-equality boundary.

- ✅ **AC8** — `Done` task, `CompletedUtc` exactly one day after `PromisedDate` ⇒ `PromiseKept=false, PromiseBroken=true, Verdict="BROKE promise"` — same code path, `.Date <= promised.Date` evaluates false when `CompletedUtc` is one day later, so `PromiseKept=false`/`PromiseBroken=!false=true`. `GM-014.json`: `promisedDate: "2026-07-29..."`, `completedUtc: "2026-07-30T09:05:37..."` (one day later) → output `{"PromiseKept": false, "PromiseBroken": true, "Verdict": "BROKE promise"}` — matches.

- ✅ **AC9** — non-`Done` task, `PromisedDate.Date == today` ⇒ `PromiseBroken=false` — `PlanningService.cs:308`: `row.PromiseBroken = promised.Date < now.Date;` (strict `<`). `GM-015.json`: `anchor_date: "2026-07-30"`, `promisedDate: "2026-07-30..."` (same day) → `2026-07-30 < 2026-07-30` is false → output `{"PromiseBroken": false, "Verdict": "Promise pending"}` — matches; confirms the same-day promise is not yet broken.

- ✅ **AC10** — non-`Done` task, `PromisedDate.Date` one day before today ⇒ `PromiseBroken=true, Verdict="BROKE promise"` — same line: `2026-07-29 < 2026-07-30` is true. `GM-016.json` matches this shape, output `{"PromiseBroken": true, "Verdict": "BROKE promise"}`.

- ✅ **AC11** — two promise notes, newest by `CreatedUtc` wins regardless of `PromisedDate` value — `PlanningService.cs:274-277`: `t.Notes.Where(n => n.IsPromise && n.PromisedDate.HasValue).OrderByDescending(n => n.CreatedUtc).FirstOrDefault();` — orders strictly by `CreatedUtc`, not by `PromisedDate`. `GM-017.json`: older note `CreatedUtc` earlier with past `promisedDate: "2026-07-27"`, newer note `CreatedUtc` later with future `promisedDate: "2026-08-03"` → output `{"latestPromisedDate": "2026-08-03...", "PromiseBroken": false, "Verdict": "Promise pending"}` — confirms the older, would-be-broken promise is fully superseded, not merged/averaged.

- ✅ **AC12** — genuine 3-key sort tie reproduces the legacy's captured relative order, no invented tie-break — `PlanningService.cs:316-320`: `rows.OrderByDescending(r => r.PromiseBroken).ThenByDescending(r => r.IsOverdue).ThenBy(r => r.Deadline ?? DateTime.MaxValue).ToList();` has exactly the three documented keys and nothing else (no `.ThenBy(WorkItemId)` or similar invented key). The initial fetch (`PlanningService.cs:264-269`, `db.WorkItems...Where(t => t.ProjectId == projectId).ToListAsync()`) also has **no explicit `ORDER BY`**, matching the legacy source's own unordered fetch — so ties are resolved purely by whatever order the DB returns rows in, combined with .NET's `OrderBy`/`ThenBy` guaranteed **stable sort** (original relative order preserved among ties). `GM-018.json`: two tasks sharing `Deadline`/broken/overdue, `taskAId: 1` ("Task A") vs `taskBId: 2` ("Task B") → expected `"workItemIdOrder": [1, 2]`. Since `Program.cs`/`PlanningDbContextFactory.cs` both still target `UseSqlite(...)` (confirmed directly), the same DB engine as the legacy capture is in play, and the query shape is unchanged — this reproduces the same non-deterministic-but-consistent mechanism as the legacy system, not an invented tie-break. ⚠️ Caveat noted below.

- ✅ **AC13** — `GetAccountabilityForAllProjectsAsync` orders by `ProjectName` before `Deadline` — `PlanningService.cs:333-338`: `rows.OrderByDescending(r => r.PromiseBroken).ThenByDescending(r => r.IsOverdue).ThenBy(r => r.ProjectName).ThenBy(r => r.Deadline ?? DateTime.MaxValue).ToList();` — the extra `ThenBy(r => r.ProjectName)` key sits exactly where FR3 requires (after `IsOverdue`, before `Deadline`). No `GM-008`–`GM-018` fixture exercises a two-project tie directly (all 11 are single-project scenarios), so this criterion is verified structurally against the FR3 spec text rather than fixture-backed like AC2–AC12.

- ✅ **AC14** — `ProjectDetail.razor`'s Accountability section — verified by reading the real file directly (the payload excerpt was truncated before this section). Lines 246–283: an "Accountability" heading and a table with `<th>Task</th><th>Assignee</th><th>Status</th><th>Deadline</th><th>Promised</th><th>Verdict</th>`, `@foreach (var row in _accountabilityRows)`, `<MudChip ... Color="@VerdictColor(row.Verdict)">@row.Verdict</MudChip>`. `VerdictColor` (lines 334-341): `"Kept promise" => Color.Success, "BROKE promise" => Color.Error, "Overdue (no promise)" => Color.Warning, "Promise pending" => Color.Info, _ => Color.Default` — exact FR5 mapping. `_accountabilityRows = await PlanningService.GetAccountabilityReportAsync(Id);` (both `OnInitializedAsync` line 321 and `RefreshAsync` line 331) — server-sorted, no client `.OrderBy`/`.Sort()` call anywhere in the markup.

- ✅ **AC15** — `/accountability` spans every project — `Accountability.razor`: `@page "/accountability"`, `<th>Project</th>` first column, `@row.ProjectName` first cell, `_rows = await PlanningService.GetAccountabilityForAllProjectsAsync();`. `MainLayout.razor`: `<MudNavLink Href="/accountability" Icon="@Icons.Material.Filled.Assessment">Accountability</MudNavLink>`.

- ✅ **AC16** — adding a note reloads Accountability with no manual refresh — verified by reading the real `ProjectDetail.razor` directly. `TaskRow.razor`'s `AddNoteAsync`: after `_notes = await PlanningService.GetNotesForTaskAsync(WorkItem.Id);` calls `await NoteAdded.InvokeAsync();`. `ProjectDetail.razor` wires both task-row loops identically: `<TaskRow WorkItem="task" StatusChanged="RefreshAsync" Meetings="_meetings" NoteAdded="RefreshAsync" />`. `RefreshAsync` (lines 324-331) includes `_accountabilityRows = await PlanningService.GetAccountabilityReportAsync(Id);` — Blazor Server re-renders automatically once the `EventCallback` completes, no page reload needed.

- ✅ **AC17** — no act/dismiss control on any verdict; no filter/refresh on `/accountability`; "Assignee" not "Owner" on both surfaces — `Accountability.razor`'s entire body (48 lines) contains only `<td>` text/`MudChip` display cells, no buttons or click handlers, and no `MudSelect`/project-filter or "Refresh" `MudButton` anywhere on the page. `ProjectDetail.razor`'s Accountability section (lines 246-283) is likewise pure `<td>` display with no controls. Column labels: `Accountability.razor` line 23 `<th>Assignee</th>`; `ProjectDetail.razor` line 262 `<th>Assignee</th>` — both consistent, never "Owner." (Note: `ProjectDetail.razor` line 129's pre-existing, unrelated Planner Grid header row does say `<th>Owner / status</th>`, but that belongs to a different, already-shipped table from an earlier backlog item — not one of the "two surfaces" NFR2/AC17 scope to the Accountability feature.)

⚠️ Edge case on AC12: the captured tie-order depends on SQLite's undocumented (but empirically consistent, rowid-based) row-return order for a query with no `ORDER BY`, not on an explicit deterministic key. This is a faithful reproduction of the legacy's own fragility (same query shape, same engine), not a new risk the rebuild introduced — but it's worth a code comment noting the missing `ORDER BY` on `PlanningService.cs:264-269` is intentional (preserves fixture-pinned tie order), so a future "cleanup" pass doesn't add one and silently reorder ties.

## Non-Functional Requirements (spot-check)

- **NFR1** (DbContext lifetime) — both new methods open `await using var db = await _dbFactory.CreateDbContextAsync();` (`PlanningService.cs:260`, `:326`), matching the other 15 methods' pattern.
- **NFR3** (`now = DateTime.UtcNow`) — `PlanningService.cs:262`: `var now = DateTime.UtcNow;` — confirmed, not a local-time conversion.
- Edge cases from spec.md all confirmed in code: promise with null `PromisedDate` excluded by `n.IsPromise && n.PromisedDate.HasValue` (`:275`); unassigned task → `t.Assignee?.FullName ?? "(unassigned)"` (`:284`); no-deadline sort fallback `r.Deadline ?? DateTime.MaxValue` present in both sorts.

## Test Results

No tests configured. Confirmed directly: `ManagerPlanner.sln` references only `ManagerPlanner.Core.csproj` and `ManagerPlanner.Web.csproj` — no `ManagerPlanner.Tests`-style xUnit project exists anywhere in this repo (only `.specclaw/baseline/harness/` and `.specclaw/parity-harness/`, which are golden-master capture/comparison tools, not a wired-in regression suite). The payload's Test Output section was empty for the same reason. AC2–AC13 were independently re-verified in this pass by hand-tracing the real `PlanningService.cs`/`Reports.cs` source against the 11 fixtures in `.specclaw/baseline/fixtures/GM-008.json`–`GM-018.json`, not by running an automated test.

**Build Output** (independently re-run, matches payload):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Issues Found

1. **No automated regression tests protect the Verdict precedence chain or the two sort orders.** `scenarios.md` itself calls GM-010 (the CQ-019 quirk, AC4) "the scenario a rebuild developer would be most tempted to 'fix' on sight," and no xUnit test project exists in this repo to catch that regression if a future change reorders `Reports.cs`'s `Verdict` getter or adds an `ORDER BY` to the `WorkItems` fetch that would perturb AC12's tie order. **Fix:** add a small `ManagerPlanner.Tests` xUnit project asserting the 11 `GM-008`–`GM-018` fixtures as permanent regression cases (mirroring the legacy repo's own `PlanningServiceTests.cs` pattern).
2. **AC12's captured tie order rests on an implicit DB-engine behavior (SQLite's no-`ORDER BY` row order), not an explicit key** — not a defect (it faithfully mirrors the legacy's own behavior), but undocumented in code. **Fix:** add a one-line comment at `PlanningService.cs:264` noting the missing `ORDER BY` is intentional, to prevent a future "cleanup" from silently changing fixture-pinned tie order.

## Summary

**Passed:** 17/17 criteria
**Failed:** 0/17 criteria
**Verdict:** PASS
