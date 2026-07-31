# Baseline Seams: Manager Planner / Executive Planning

**Date generated:** 2026-07-30
**Grounded in:** .specclaw/analysis/domain-model.md, codebase-report.md, architecture.md,
functional-spec.md, rebuild-backlog.md (all present), plus direct reads of the legacy source at
`../manager-planner` (the actual legacy repo the analysis docs describe — `manager-planner-mod`
is the rebuild repo and does not itself contain `ExecutivePlanning.Core`/`ExecutivePlanning.Desktop`/
`ManagerPlanner.Desktop`), and `.specclaw/analysis/clarifications.md` / `decisions.md` /
`.specclaw/adr/000{1,4}-*.md` for cross-referencing.

## Seam Ranking

### Pure function (lowest cost, highest fidelity — capture these first)

1. **`PlanningRules.ValidateProjectName(string?)`** — `Services/PlanningValidation.cs:21-27`.
   No DB, no clock. Rejects empty/whitespace; rejects length > `MaxProjectName` (120).
2. **`PlanningRules.ValidateTaskTitle(string?)`** — `:29-35`. Same shape, `MaxTaskTitle` (120).
3. **`PlanningRules.ValidateObjectiveTitle(string?)`** — `:37-43`. `MaxObjectiveTitle` (150).
4. **`PlanningRules.ValidateChecklistLabel(string?)`** — `:45-51`. `MaxChecklistLabel` (300).
5. **`PlanningRules.ValidateNoteText(string?)`** — `:53-59`. `MaxNoteText` (2000).
6. **`PlanningRules.ValidateNoteDate(DateTime, DateTime? nowUtc = null)`** — `:65-75`. Notable:
   unlike every other clock read in this codebase, this one method **already accepts an injectable
   `nowUtc` override** — calling it directly (bypassing `PlanningService.AddNoteAsync`, which does
   *not* forward an override — see Capture Blockers CB-2) makes it a fully pure, deterministic
   function today, with zero rebuild-side changes required to pin "today." This is the cheapest,
   highest-fidelity seam in the whole codebase.
7. **`AccountabilityRow.Verdict` (get-only property)** — `Services/Reports.cs:37-47`. Pure
   precedence logic over already-set fields (`PromiseKept`, `PromiseBroken`, `IsOverdue`,
   `LatestPromisedDate`) — no DB access, no clock read *inside the getter itself*. This is the
   single highest-value pure-function seam in this codebase: rebuild-backlog.md item 8 calls the
   Verdict computation "the single highest-priority verification input in this backlog," and its
   entire string-precedence logic (the CQ-019/CQ-024-flagged nuance included) can be pinned with
   zero database setup at all by constructing an `AccountabilityRow` directly and reading
   `.Verdict` — see GM-008 through GM-012 in scenarios.md. (The upstream computation of
   `PromiseKept`/`PromiseBroken`/`IsOverdue` *from* real `WorkItem`/`ProgressNote` rows is a
   separate, stateful-service-boundary seam — listed below.)
8. **`ProjectSummary.PercentComplete` (get-only property)** — `Services/Reports.cs:63`:
   `TotalTasks == 0 ? 0 : Math.Round(100.0 * Done / TotalTasks, 1)`. Pure, no DB, no clock. This
   **directly resolves CQ-020** (previously flagged as "no document states the rounding rule") —
   the source itself states it: round to 1 decimal place via `Math.Round`'s default
   `MidpointRounding.ToEven` (banker's rounding). See GM-034 through GM-038, including a
   deliberately-chosen exact-midpoint case (GM-038) that pins the ToEven tie-break concretely.

### Stateful service boundary (medium cost, high fidelity — needs a DB arrange step)

Every one of these is a public method on `PlanningService` (`Services/PlanningService.cs`), which
both desktop apps call directly with no repository/interface layer in between (confirmed directly
in `App.axaml.cs` of both apps and in architecture.md's L3 finding). All are exercisable today via
the exact arrange pattern already established in `tests/ExecutivePlanning.Tests/TestDb.cs` (a real
in-memory SQLite connection kept open for the test's lifetime) — see "Recommended Seam" below.

- `AddProjectAsync` (`:54-61`), `DeleteProjectAsync` (`:64-70`), `DeleteTaskAsync` (`:73-79`)
- `GetTasksForProjectAsync` (`:82-86`), `GetTaskAsync` (`:88-93`), `AddTaskAsync` (`:95-114`)
- `GetObjectivesForProjectAsync` (`:117-120`), `AddObjectiveAsync` (`:122-130`)
- `GetPlannerForProjectAsync` (`:136-143`)
- `AddChecklistItemAsync` (`:146-159`), `ToggleChecklistItemAsync` (`:162-169`)
- `SetOwnersAsync` (`:172-179`)
- `ChangeStatusAsync` (`:184-205`)
- `GetMeetingsForProjectAsync` (`:208-212`), `AddMeetingAsync` (`:214-228`)
- `AddNoteAsync` (`:234-254`), `GetNotesForTaskAsync` (`:256-261`)
- `GetAccountabilityReportAsync` (`:269-330`) — the flagship computation; drives
  `PromiseKept`/`PromiseBroken`/`IsOverdue` from real `WorkItem`+`ProgressNote` rows, then the
  broken-first/overdue-first/soonest-deadline sort (see Capture Blocker CB-4 for the undocumented
  tie-break CQ-024 asks about).
- `GetAccountabilityForAllProjectsAsync` (`:333-346`), `GetProjectSummaryAsync` (`:348-366`)
- `GetUsersAsync`/`GetTeamMembersAsync`/`AddUserAsync` (`:33-46`)
- `HasAnyData`/`LoadSampleDataIfEmpty`/`ResetSampleData` (`:19-30`), which wrap:
- **`DbSeeder.Seed`/`SeedIfEmpty`/`ResetToSampleData`** (`Data/DbSeeder.cs`, full file, 292 lines) —
  a static method taking `PlanningDbContext` directly, same cost/fidelity profile as the rest of
  this class. Worth calling out on its own: this single method populates nearly every entity type
  in one call, so it is a cheap way to golden-master the *structural* shape rebuild-backlog.md's
  CQ-023 asks about (see GM-041/GM-042/GM-043) — but its output is **not** clock-safe end-to-end
  (see Capture Blocker CB-5); scenarios built on it must restrict assertions to structural counts,
  not date-derived verdicts.

### Data/persistence boundary (medium cost, medium fidelity — cascade/SetNull/Restrict rules)

Every rule below is configured in `Data/PlanningDbContext.cs`'s `OnModelCreating` (read in full,
24-195) and is exercisable by manipulating `PlanningDbContext` directly — the exact pattern the
existing test suite already uses for cases `PlanningService` has no dedicated method for (e.g.
`Deleting_project_cascades_to_tasks_and_notes` calls `t.Db.Projects.Remove(project)` directly).
Several of these rules (Objective, User, Meeting deletion) have **no `PlanningService` method at
all** — domain-model.md flags this explicitly for `User`/`Objective` deletion — but the rule is
still real, schema-configured behavior, reachable and capturable via direct `DbContext`
manipulation; it is simply never routed through either desktop UI.

- `Project` → `Objective`, Cascade (`:89-93`)
- `Project` → `WorkItem`, Cascade (`:59-62`)
- `Project` → `Meeting`, Cascade (`:143-146`)
- `WorkItem` → `Assignee` (`User`), SetNull (`:65-68`)
- `WorkItem` → `DiscoveredInMeeting` (`Meeting`), SetNull (`:71-74`)
- `WorkItem` → `Objective`, SetNull (`:77-80`)
- `ChecklistItem` → `WorkItem`, Cascade (`:102-105`)
- `ChecklistItem.Parent` self-reference, **Restrict** (`:109-112`) — per the code comment,
  deliberately not Cascade "to avoid multiple cascade paths on SQLite." Capturable directly (see
  GM-032), but flagged with a caveat: CQ-014 already establishes that no application code path
  (UI or `PlanningService`) ever exercises single-`ChecklistItem` deletion — the harness can only
  pin "attempting this against the raw schema throws," not any application-level feature built on
  top of it. See "No Legacy Behaviour Exists" in scenarios.md for the distinction.
- `ChecklistItem` → `Assignee` (`User`), SetNull (`:114-117`)
- `TaskOwner` → `WorkItem`, Cascade (`:125-128`); `TaskOwner` → `User`, Cascade (`:130-133`)
- `Meeting` → `Participant` (`User`), SetNull (`:148-151`)
- `ProgressNote` → `WorkItem`, Cascade (`:162-165`)
- `ProgressNote` → `Meeting`, SetNull (`:168-171`)
- `ProgressNote` → `Author` (`User`), **Restrict** (`:173-176`)
- `StatusChange` → `WorkItem`, Cascade (`:185-188`)
- `StatusChange` → `ChangedBy` (`User`), **Restrict** (`:190-193`)
- `User` → `Project` (`Owner`), **Restrict** (`:44-47`) — domain-model.md notes "no `DeleteUserAsync`
  exists in `PlanningService` at all, so this restriction is currently untestable through either
  UI" — true for the UI, but directly testable against `DbContext` (see GM-030).

## Excluded: UI Automation

Both desktop UI layers (`ExecutivePlanning.Desktop`'s tabbed Avalonia UI, `ManagerPlanner.Desktop`'s
hand-rolled Win95-style MDI shell) are excluded from the golden-master harness entirely. Grounds:

- **The rebuild's target platform is a Blazor web app, not a desktop UI framework** —
  `.specclaw/adr/0001-target-platform-blazor-web.md`: "Desktop-only concerns — the MDI shell,
  drag/resize/tile window chrome, the hand-rolled `MessageBox` (backlog items 12–13) — **do not
  port**; they are re-interpreted as web navigation (see ADR-0004), not reproduced." A UI-driven
  test that drives Avalonia's `MdiWindow`/`MdiHost`/`MessageBox` would carry forward a paradigm
  (draggable child windows, cascade/tile) that ADR-0004 explicitly states "do not exist" in the web
  target: "In a **web app these concepts do not exist**: there are no draggable child windows, no
  tile/cascade, no per-window minimise." A golden master for behavior the target platform cannot
  even represent would be worthless capture effort.
- **Zero automated test coverage exists for either UI layer today**, confirmed directly:
  `tests/ExecutivePlanning.Tests.csproj`'s only `ProjectReference` is to `ExecutivePlanning.Core.csproj`
  (codebase-report.md, Risks/Tech-Debt) — there is no existing pattern to imitate even if UI capture
  were in scope.
- **The Win95 skin itself is being dropped**, not carried forward — `decisions.md` CQ-006: "Option 2
  — Modernize the application using a clean, modern UI theme. The Win95-style appearance is not
  considered a required part of the legacy business behaviour... preferably MudBlazor." Capturing
  the exact pixel/drag/resize behavior of chrome the rebuild has already decided to discard would
  not inform the rebuild.
- The one exception worth flagging: rebuild-backlog.md item 12's verification need ("manual/golden-
  master captures of exact drag/resize/maximize/restore/z-order behavior") is CQ-025, already
  logged in clarifications.md as a DATA question — but per ADR-0004 this MDI chrome is being
  **re-interpreted as web navigation, not reproduced**, so that capture (if ever done) would inform
  a UX comparison, not a behavioral-equivalence golden master, and is out of scope for this harness.

## Capture Blockers (Determinism Audit)

### CB-1 — Unguarded `DateTime.UtcNow` writes with no injected clock

| Site | What it affects | Injectable today? |
|---|---|---|
| `WorkItem.CreatedUtc` default initializer — `Domain/WorkItem.cs:15` | Not read by any documented business rule; cosmetic bookkeeping only, not overridden by `AddTaskAsync` | No |
| `Project.CreatedUtc` default initializer — `Domain/Project.cs:12` | Feeds `GetProjectsAsync`'s `OrderByDescending(p => p.CreatedUtc)` (`PlanningService.cs:51`) — an ordering concern, see CB-4 | No |
| `ProgressNote.CreatedUtc` default initializer — `Domain/ProgressNote.cs:15`, not overridden by `AddNoteAsync`'s object initializer (`PlanningService.cs:241-250`) | Directly feeds Rule 7's "most recently created promise wins" ordering (`OrderByDescending(n => n.CreatedUtc)`, `PlanningService.cs:285`) | No |
| `StatusChange.ChangedUtc` default initializer — `Domain/StatusChange.cs:13`, not overridden by `ChangeStatusAsync`'s initializer (`PlanningService.cs:191-198`) | Audit-trail ordering/exact-value only | No |
| `Meeting.MeetingDate` default initializer — `Domain/Meeting.cs:13` | **Assessed, not a real concern**: `AddMeetingAsync` always receives an explicit `meetingDate` parameter (`PlanningService.cs:214-222`) that overrides the default — checked directly, not assumed. | N/A |
| `PlanningService.ChangeStatusAsync`, line 201: `task.CompletedUtc = newStatus == Done ? DateTime.UtcNow : null;` | Feeds Rule 7's `PromiseKept`/`PromiseBroken` comparison directly — **highest-priority determinism concern**, tied to CQ-019/CQ-024 | No |
| `PlanningService.ToggleChecklistItemAsync`, line 167: `item.CompletedUtc = isDone ? DateTime.UtcNow : null;` | Rule 11 — no documented rule reads this value back for a decision, cosmetic timestamp only | No |
| `PlanningService.GetAccountabilityReportAsync`, line 271: `var now = DateTime.UtcNow;` | Drives `IsOverdue` and the non-Done branch of `PromiseBroken` — **the core clock read for Rule 7's flagship computation** | No |
| `PlanningService.GetProjectSummaryAsync`, line 350: `var now = DateTime.UtcNow;` | Drives `ProjectSummary.Overdue`'s count | No |
| `DbSeeder.Seed` (`Data/DbSeeder.cs`, throughout) | Every seeded deadline/meeting-date/note-date/promised-date is `DateTime.UtcNow.AddDays(...)`/`.AddMonths(...)` — the entire "realistic kept/broken/pending demo" dataset's temporal shape is relative to real wall-clock time at seed time, with **no injectable override anywhere** in `DbSeeder`'s signature | No |

**Mitigation:**
- For the two writes feeding Rule 7 directly (`ChangeStatusAsync`'s `CompletedUtc`,
  `GetAccountabilityReportAsync`'s `now`): **Option 1 — record the capture timestamp in the
  fixture and require the rebuilt app to accept an injectable clock.** Given rebuild-backlog.md's
  explicit framing of this as "the single highest-priority verification input in this backlog"
  (CQ-024) and the CQ-019 mislabeling defect riding on the same computation, anything less than
  pinning the exact "now" used at capture risks a fixture that looks wrong on replay for reasons
  that have nothing to do with the rebuild's correctness. **This implies an ADR in the new repo**
  (a `Clock`/`TimeProvider` abstraction `PlanningService`'s rebuilt equivalent must accept).
- For `ToggleChecklistItemAsync`'s `CompletedUtc` and the various `CreatedUtc`/`ChangedUtc` default
  initializers not tied to a decision-affecting rule: **Option 2 — normalise the field out of
  comparison.** Record the value, but assert only non-null/null-ness (rule 11, rule 9's own
  "clears back to null" half), not the literal timestamp. Cheaper, and loses nothing rule 9/11
  actually describe.
- For `DbSeeder`: since none of GM-041/042/043 (this design's sample-data scenarios) assert on any
  date-derived Verdict/Overdue outcome — only on structural counts/existence — no mitigation is
  required for those specific scenarios. If a future scenario ever needs `DbSeeder`'s emergent
  Verdict shape (e.g. "the seeded 'broken promise' task really does show BROKE promise"), that
  would need Option 1 or 3 applied to `DbSeeder` itself, which today has no clock parameter to hang
  either mitigation on — **this is exactly a TARGET-GAP-shaped question for `/specclaw:clarify`**:
  should the rebuild's seeder accept an injectable anchor date so its own demo data is
  golden-masterable? Not currently asked in `clarifications.md`; recommend adding it if the rebuild
  intends to golden-master seeded-data verdicts, not just seeded-data counts.
- **Cross-reference:** `clarifications.md` already carries CQ-019 (the mislabeling defect) and
  CQ-024 (the truth-table/tie-break DATA question) as the two questions most directly entangled
  with this finding — both still unanswered and blocking, per that file's own summary.

### CB-2 — Business-rule computations comparing a stored date against "today"

- **Rule 6 (`ValidateNoteDate`)** — genuinely low-risk: the method **already accepts an injectable
  `nowUtc`** (`PlanningValidation.cs:65`), so calling it directly (as the pure-function seam does)
  needs no mitigation at all — pin `nowUtc` to any fixed value in the scenario and the result is
  100% reproducible. The one thing worth flagging: `PlanningService.AddNoteAsync` (line 240) calls
  `ValidateNoteDate(effectiveDate)` **without** forwarding an override, so a scenario exercising the
  full `AddNoteAsync` service path (not the bare validator) still needs Option 3 (express the note
  date as an offset from "whenever the harness runs," e.g. "3 days before now," so accept/reject
  stays correct regardless of the calendar date the fixture is replayed on) — the exact rejection
  *message text* embeds the formatted `earliest` date, though, so a byte-exact message-string
  fixture captured via `AddNoteAsync` would need Option 1 (or must be captured via the bare
  validator with a pinned `nowUtc` instead, sidestepping the problem entirely — recommended).
- **Rule 7 (`GetAccountabilityReportAsync`'s `IsOverdue`/`PromiseBroken`)** — no override exists at
  all; see CB-1's Option 1 recommendation above. This is the priority case.
- **`ProjectSummary.Overdue`** (`GetProjectSummaryAsync`) — same "now" comparison shape as Rule 7
  but feeding a simple count, not a subtle five-way precedence chain. **Option 3** (express the
  scenario's deadlines as offsets from "now" at run time) is judged sufficient here — reserving the
  costlier Option 1 specifically for the Verdict computation itself, rather than reflexively
  applying the priciest mitigation everywhere.

### CB-3 — Auto-increment identity/primary-key values

**Assessed, not a live concern for this harness** — checked directly, not assumed. Every scenario
runs against a **fresh, empty in-memory SQLite database** built by the same pattern
`tests/ExecutivePlanning.Tests/TestDb.cs` already uses (`SqliteConnection("DataSource=:memory:")`,
opened once, kept alive for the fixture's lifetime). SQLite's `rowid`/autoincrement always starts at
1 for a fresh table and increments by insertion order within a single connection with no concurrent
writers — since the harness's arrange sequence is fixed code (not user-timed input), the same
scenario produces the same IDs on every capture and every replay. No mitigation needed, provided the
harness's arrange order is never made conditional on wall-clock time or external input.

### CB-4 — Collections returned without a stable `ORDER BY` / tie-break

- `GetUsersAsync`/`GetTeamMembersAsync` — `OrderBy(u => u.FullName)` only (`PlanningService.cs:34,38`).
  Assessed risk (ties on `FullName` are unresolved), but none of this design's scenarios introduce a
  same-name tie — no live mitigation needed yet; flagged for awareness if a future scenario adds one.
- `GetProjectsAsync` — `OrderByDescending(p => p.CreatedUtc)` only (`:51`), **no secondary key**.
  Combined with CB-1's `Project.CreatedUtc` clock issue, two projects created within the same tick
  have an undefined tie order per the SQL semantics (in practice SQLite/EF Core's execution is
  observed to be stable for a single-writer connection, but this is an implementation detail, not a
  guarantee in the legacy code). **Mitigation: Option 2** — compare scenario results tolerant of
  same-`CreatedUtc` ties (e.g. assert set membership plus each project's own fields, not strict
  list-position equality) unless a scenario deliberately needs the tie order itself.
- `GetTasksForProjectAsync` — `OrderBy(t => t.Deadline ?? DateTime.MaxValue)` only (`:85`), no
  secondary key — two tasks sharing a deadline (or both null) have an undefined tie order. Same
  Option 2 treatment. **Confirmed doubly relevant**: `ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:101`
  re-sorts `o.Tasks` client-side with the *identical* `OrderBy(t => t.Deadline ?? DateTime.MaxValue)`
  key — read directly, this is not a guess — so the ViewModel duplicates the same tie-break gap
  rather than resolving it.
- **`GetAccountabilityReportAsync`/`GetAccountabilityForAllProjectsAsync`'s sort — the priority
  finding.** `OrderByDescending(PromiseBroken).ThenByDescending(IsOverdue).ThenBy(Deadline ?? MaxValue)`
  (`PlanningService.cs:326-328`, plus `.ThenBy(ProjectName)` for the all-projects variant,
  `:341-344`) has **no tie-break beyond the last listed key** — this is exactly the gap
  rebuild-backlog.md's CQ-024 flags: "the doc states the three sort keys but not tie-breaking
  behavior beyond them." Rather than assuming or inventing a tie-break rule, GM-018 in
  scenarios.md deliberately constructs a genuine tie (two broken-and-overdue tasks sharing an
  identical deadline) and records whatever order the legacy system's actual SQLite/EF Core
  execution produces as the golden master — this *is* the capture CQ-024 asks for, at least for one
  representative tie shape.
- `GetMeetingsForProjectAsync` (`OrderByDescending(m => m.MeetingDate)`, `:211`) and
  `GetNotesForTaskAsync` (`OrderByDescending(n => n.NoteDate)`, `:260`) — both lack a tie-break;
  assessed risk, not exercised by any scenario in this design with a genuine same-date tie, flagged
  for awareness only.
- `GetPlannerForProjectAsync`'s `.Include(o => o.Tasks)` and `.Include(t => t.Checklist)` navigation
  collections (`:140-142`) have **no explicit `OrderBy` at all** for the included collections
  (unlike `Objective`'s own top-level `.OrderBy(o => o.SortOrder)`, `:139`) — checked directly, this
  is a real gap in this query. **Read directly to resolve, not guessed:**
  `ManagerPlanner.Desktop/ViewModels/RowViewModels.cs:69-84` (`TaskRowVm`'s `BuildTree`) *does*
  re-sort the nested `ChecklistItem` tree client-side via `.OrderBy(c => c.SortOrder)` at both
  levels, compensating for the gap at the ViewModel layer — but this compensation happens outside
  the `PlanningService` seam this harness targets. A scenario calling `GetPlannerForProjectAsync`
  directly (bypassing the ViewModel) would see checklist items in raw database-scan order, not
  `SortOrder` order. This is a genuine seam-boundary nuance worth a `/specclaw:clarify` TARGET-GAP
  question: should the rebuild's equivalent service method apply `.OrderBy(SortOrder)` itself
  (making the ordering guarantee part of the service contract), or is it acceptable to require every
  consumer to re-sort client-side as the legacy ViewModel does today?

### CB-5 — `DbSeeder`'s entire temporal shape is unanchored (see CB-1)

Called out separately because it is not a single-field issue like the rest of CB-1: essentially
every date in the seeded dataset (`Data/DbSeeder.cs`) is `DateTime.UtcNow.AddDays(...)`/
`.AddMonths(...)`, meaning the *whole demo dataset's* Verdict/Overdue shape (which tasks look
"broken," "kept," "overdue," etc.) shifts with real wall-clock time at seed time, with no
injectable override anywhere in `DbSeeder`'s public surface. Mitigated for this design by scoping
GM-041/042/043 to structural counts only (see CB-1's mitigation entry).

## Recommended Seam

**Primary target: `PlanningService`'s public async methods (the stateful service boundary),
arranged via the exact real in-memory-SQLite `TestDb` pattern already proven in
`tests/ExecutivePlanning.Tests/TestDb.cs`.** This is the single seam through which every one of
`domain-model.md`'s 11 numbered business rules is reachable, it already has a working, idiomatic
arrange pattern to imitate rather than invent, and it requires no UI automation at all.

**Within that seam, prioritize the two pure-function sub-targets first**, since they need no
database arrange step at all and are the cheapest, highest-fidelity captures available in this
codebase:
1. `AccountabilityRow.Verdict` (construct the row directly, set its booleans/dates by hand, read
   `.Verdict`) — nails the CQ-019/CQ-024 precedence-order truth table with zero DB setup.
2. The six `PlanningRules` validators, called directly — especially `ValidateNoteDate`, which
   already supports a pinned `nowUtc`.

**Supplement with the Data/persistence boundary** (direct `PlanningDbContext` manipulation,
matching the existing tests' own pattern) specifically for the cascade/SetNull/Restrict rules that
have no `PlanningService` method to drive them (Objective, Meeting, and User deletion).

**UI automation is excluded entirely** — see rationale above; nothing in either desktop shell
carries forward to the Blazor rebuild per ADR-0001/ADR-0004.

This document does not run the legacy app and does not capture any fixture itself — a human runs
the actual capture (`--harness`, then a real `dotnet test`/`--record` pass) as a separate,
later step, after confirming this recommended seam.
