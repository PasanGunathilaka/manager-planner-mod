# Verification Report: task-deletion

**Verified:** 2026-08-03
**Model:** Claude Sonnet 5
**Verdict:** PASS

## Quotes (evidence extracted before judging)

- AC2 (spec.md): "Deleting a task that has a nested checklist (a parent item plus a child item), a progress note, a status-change history row, and an owner leaves zero rows in `WorkItems`, `ChecklistItems`, `ProgressNotes`, and `TaskOwners` for that task, confirmed by direct database inspection — matching `GM-025` exactly."
- FR1 (spec.md): "it must load `.Include(w => w.Checklist)` before removing... a plain `FindAsync` + `Remove`... throws `SQLite Error 19: FOREIGN KEY constraint failed` against a fresh `IDbContextFactory`-created context whenever the task has a nested checklist item."
- Actual code (`src/ManagerPlanner.Core/Services/PlanningService.cs:353-362`):
  ```
  public async Task DeleteTaskAsync(int taskId)
  {
      await using var db = await _dbFactory.CreateDbContextAsync();

      var t = await db.WorkItems.Include(w => w.Checklist).FirstOrDefaultAsync(w => w.Id == taskId);
      if (t is null) return;

      db.WorkItems.Remove(t);
      await db.SaveChangesAsync();
  }
  ```
- `PlanningDbContext.cs:132-135` (AC6 basis): `e.HasOne(x => x.User).WithMany(u => u.OwnedTasks).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);`
- Build output: "Build succeeded. 0 Warning(s) 0 Error(s)"

## Independent verification performed (beyond reading the payload/code)

The task instructions flagged that this repo's prior verify runs had truncated file excerpts, and that this change's single most important fact — whether `.Include(w => w.Checklist)` genuinely exists in `DeleteTaskAsync` — must be re-checked directly, not deferred to the build's own self-report. I did the following, independently of the prior agent's own T1/T2 verification runs:

1. **Read the real file directly** (`src/ManagerPlanner.Core/Services/PlanningService.cs`, not the payload's truncated 363/376-line excerpt) — confirmed `DeleteTaskAsync` (lines 353-362) genuinely contains `.Include(w => w.Checklist)` before `FirstOrDefaultAsync`/`Remove`, exactly as FR1/the code's own doc comment (lines 341-352) describe. **This is real, not just claimed.**
2. **Ran `dotnet build` myself** — reproduced "Build succeeded. 0 Warning(s), 0 Error(s)" independently of the payload's build-output excerpt.
3. **Wrote a fresh, self-contained console harness** (not reusing the prior agent's `T1Verify` scratch project) referencing the real `ManagerPlanner.Core` project, using a real `IDbContextFactory<PlanningDbContext>` (fresh context per call, matching production exactly) against a brand-new temp SQLite file (not the app's dev DB). Seeded the exact AC2 shape — a task with a nested checklist (one parent + one child item), one `ProgressNote`, one `StatusChange`, and one `TaskOwner` — then called the real `PlanningService.DeleteTaskAsync` from a **separate, freshly-created context** than the one that seeded the data (to guarantee no incidental change-tracking carries over). Result:
   ```
   [seed check] checklist=2 (expect 2), notes=1 (expect 1), status=1 (expect 1), owners=1 (expect 1)
   [AC2] DeleteTaskAsync succeeded (no FK exception) for the nested-checklist task.
   [AC2] Post-delete counts — WorkItems=0, ChecklistItems=0, ProgressNotes=0, StatusChanges=0, TaskOwners=0
   [AC2] PASS — zero rows remain across WorkItems/ChecklistItems/ProgressNotes/TaskOwners.
   [edge case] Project survives deletion: True (expect True)
   [AC5] DeleteTaskAsync succeeded for the empty task (no error).
   [AC5] Empty task still present: False (expect False)
   [edge case] Second DeleteTaskAsync call on already-deleted id: no exception (PASS).
   [edge case] DeleteTaskAsync on a never-existed id: no exception (PASS).
   ```
4. **Ran a negative control** — to rule out the possibility that my harness simply doesn't enforce SQLite foreign keys (which would make the AC2 "pass" meaningless), I reproduced the literal legacy body (`var t = await db.WorkItems.FindAsync(taskId); ... db.WorkItems.Remove(t); await db.SaveChangesAsync();` — **no** `.Include`) against an identical fresh-context/nested-checklist scenario. Result:
   ```
   [negative-control] EXPECTED FAILURE reproduced: DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
   [negative-control] Inner exception: SqliteException: SQLite Error 19: 'FOREIGN KEY constraint failed'.
   ```
   This is an exact match to spec.md's claimed failure mode, confirming both that the harness genuinely enforces the FK constraint (so the AC2 pass above is not a false positive) and that the `.Include(w => w.Checklist)` fix is necessary — the bug it addresses is real, reproducible, and specifically what the fix corrects.
5. **Cross-checked the real legacy source directly** (`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:73-79` and `../manager-planner/src/ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:239`), not just the doc summary — confirmed the legacy `DeleteTaskAsync` body is exactly `FindAsync`+guard+`Remove`+`SaveChangesAsync` as spec.md states, and the confirmation text is genuinely `$"Delete task '{title}' and its checklist and notes?\nThis cannot be undone."` verbatim (matching the rebuild's `TaskRow.razor` exactly, including the `\n`).

**Conclusion: the `.Include(w => w.Checklist)` fix is genuinely present, syntactically correct, and functionally necessary and sufficient. I do not disagree with the prior build-time verification — my independent, from-scratch reproduction corroborates it.**

## Acceptance Criteria

- ✅ **AC1** — `dotnet build` at the solution root succeeds with 0 errors. — Reproduced independently: "Build succeeded. 0 Warning(s) 0 Error(s)."
- ✅ **AC2** — Deleting a task with nested checklist + note + status-change + owner leaves zero rows in `WorkItems`/`ChecklistItems`/`ProgressNotes`/`TaskOwners`, matching `GM-025`. — Confirmed both by direct code reading (`.Include(w => w.Checklist)` genuinely present at `PlanningService.cs:357`) and by an independent live-DB run (see above): all five affected tables (including `StatusChanges`) hit zero rows after delete, with no FK exception.
- ✅ **AC3** — Clicking Delete opens a dialog with the exact confirmation text, real title interpolated; Cancel makes no `DeleteTaskAsync` call. — `TaskRow.razor:191-199`: `var confirmed = await DialogService.ShowMessageBoxAsync("Delete task", $"Delete task '{WorkItem.Title}' and its checklist and notes?\nThis cannot be undone.", yesText: "Delete", cancelText: "Cancel"); if (confirmed == true) { await PlanningService.DeleteTaskAsync(WorkItem.Id); await TaskDeleted.InvokeAsync(); }` — the service call is inside the `if (confirmed == true)` guard, so `false`/`null` (Cancel/dismiss) never calls it. Verbatim match confirmed directly against the real legacy caller (`../manager-planner/src/ManagerPlanner.Desktop/ViewModels/MainViewModel.cs:239`).
  - ⚠️ Edge case (documentation-only, not a code defect): spec.md's AC3 prose renders the text with a space ("...notes? This cannot be undone.") while FR1/FR3 and the actual code use a literal `\n`. The code matches FR3 and the verbatim legacy source exactly; the AC3 restatement is very likely just markdown-flattening of the `\n`, not a distinct requirement.
- ✅ **AC4** — Confirming delete calls `DeleteTaskAsync`, removes the row from its objective/ungrouped section, and updates Accountability with no manual refresh. — `TaskRow.razor`'s `TaskDeleted` `EventCallback` fires after a successful delete; `ProjectDetail.razor:159` and `:174` wire `TaskDeleted="RefreshAsync"` on both the per-objective loop and the Ungrouped section, and `RefreshAsync` (`ProjectDetail.razor:325-333`) reloads `_objectives`, `_ungroupedTasks`, **and** `_accountabilityRows` together — a genuine full refresh, not row-local state.
- ✅ **AC5** — Deleting a task with no checklist/notes/status-history/owners succeeds without error. — Confirmed live: `[AC5] DeleteTaskAsync succeeded for the empty task (no error)` and the row is gone afterward.
- ✅ **AC6** — `TaskOwner`→`User` cascade confirmed `DeleteBehavior.Cascade` in existing schema, no new service method added. — `PlanningDbContext.cs:132-135`: `.HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);`. `PlanningService.cs` gains only `DeleteTaskAsync` — no user-deletion method exists.
- ✅ **AC7** — Exactly eighteen `PlanningService` methods exist. — Counted directly: `GetProjectsAsync, AddProjectAsync, GetProjectSummaryAsync, GetCurrentManagerIdAsync, AddObjectiveAsync, GetPlannerForProjectAsync, AddTaskAsync, GetTeamMembersAsync, GetUngroupedTasksForProjectAsync, ChangeStatusAsync, ToggleChecklistItemAsync, GetMeetingsForProjectAsync, AddMeetingAsync, AddNoteAsync, GetNotesForTaskAsync, GetAccountabilityReportAsync, GetAccountabilityForAllProjectsAsync, DeleteTaskAsync` = 18.
- ✅ **AC8** — No project-deletion UI, no `DeleteUserAsync`, no undo/soft-delete mechanism anywhere in the diff. — Repo-wide search for `DeleteUserAsync|DeleteProjectAsync|IsDeleted|SoftDelete` under `src/` returned **no matches**; a search for "Delete" under `Components/Pages` returned only `TaskRow.razor` and `ProjectDetail.razor` (the two files this change touches).

## Test Results

No automated test project exists in this rebuild yet (no xUnit project references `ManagerPlanner.Core`); "Test Output" and "Lint Output" in the build payload were both empty, confirmed by inspection — this is a pre-existing gap in the rebuild, not something task-deletion introduced or is required to fix.

Build output (reproduced independently):
```
Determining projects to restore...
All projects are up-to-date for restore.
ManagerPlanner.Core -> ...\ManagerPlanner.Core.dll
ManagerPlanner.Web -> ...\ManagerPlanner.Web.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

In lieu of an automated test suite, I built and ran an independent live-DB verification harness (see "Independent verification performed" above) exercising the real `PlanningService.DeleteTaskAsync` against a fresh temp SQLite database with the exact AC2/AC5/edge-case shapes, plus a negative control proving the fixed bug is real. All checks passed; full harness output is included above.

## Issues Found

No blocking issues found.

1. **Minor, non-blocking: spec.md's own AC3 restatement differs cosmetically from FR1/FR3** — AC3 quotes the confirmation text with a space where FR1/FR3 and the actual code use `\n`. The code is correct against the verbatim legacy source and against FR3; this is a spec-document wording inconsistency, not a code defect. **Fix (optional, doc-only):** align AC3's quoted text with FR1/FR3's `\n` in a future spec edit, purely for internal consistency.
2. **Minor, pre-existing, out of scope: `ProjectDetail.razor`'s Planner Grid `<thead>` has no header for the Notes column** — `TaskRow.razor` renders 5 `<td>`s (Task, Owner/status, Checklist, Notes, Actions) but the `<thead>` only has 4 `<th>`s (Tasks, Owner/status, Progress checklist, Actions) — the Notes column has been headerless since the `progress-notes-and-promise-tracking` change, and this change's diff (confirmed via `git show 448e8f6`) only added the `<th>Actions</th>` correctly for its own new column, touching nothing else in the `<thead>`. Not a task-deletion regression and not covered by any of its ACs.

## Summary

**Passed:** 8/8 criteria
**Failed:** 0/8 criteria
**Verdict:** PASS
