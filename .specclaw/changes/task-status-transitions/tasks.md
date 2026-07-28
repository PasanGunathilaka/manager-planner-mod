# Tasks: Task status transitions and the StatusChange audit trail

**Change:** task-status-transitions
**Created:** 2026-07-28
**Total Tasks:** 2

## Summary

Two tasks across two waves: (1) add `ChangeStatusAsync` to
`PlanningService`, (2) wire four status buttons into `TaskRow` and refresh
`ProjectDetail` after each change, then verify end-to-end. No task adds a
`Reason` input, a `StatusChange` history view, a confirmation dialog, or
touches deletion/checklist/notes/meetings — those stay out of scope per
spec.md NFR2/AC7/AC9.

## Tasks

### Wave 1 — Core business logic

- [ ] `T1` — Add `ChangeStatusAsync` to `PlanningService`
  - Files: `src/ManagerPlanner.Core/Services/PlanningService.cs`
  - Estimate: small
  - Depends: none
  - Notes: Ground-truth against the real legacy source at
    `C:\Learnings\Projects\manager-planner\src\ExecutivePlanning.Core\Services\PlanningService.cs:184-205`,
    not the doc summary. `ChangeStatusAsync(int taskId, WorkItemStatus
    newStatus, int changedById, string? reason = null)`: load the
    `WorkItem` by id via the factory-created context (`FirstOrDefaultAsync`,
    throw `InvalidOperationException` if not found — matches legacy); `if
    (task.Status == newStatus) return;` **before** creating any
    `StatusChange` row or calling `SaveChangesAsync` (Business Rule 8 —
    this exact placement matters, don't move the no-op check after row
    creation); otherwise construct `new StatusChange { WorkItemId =
    task.Id, FromStatus = task.Status, ToStatus = newStatus, ChangedById =
    changedById, Reason = reason }`, then `task.Status = newStatus;
    task.CompletedUtc = newStatus == WorkItemStatus.Done ? DateTime.UtcNow
    : null;` (Business Rule 9 — unconditional in both directions, no `if`
    guarding the `null`-clearing branch), add the `StatusChange` to
    `db.StatusChanges`, and `SaveChangesAsync()`. Use
    `IDbContextFactory<PlanningDbContext>` like all nine existing methods.

### Wave 2 — Page + verification

- [ ] `T2` — Four status buttons on `TaskRow`, wired to refresh `ProjectDetail`, end-to-end verification
  - Files: `src/ManagerPlanner.Web/Components/Pages/TaskRow.razor`, `src/ManagerPlanner.Web/Components/Pages/ProjectDetail.razor`
  - Estimate: medium
  - Depends: `T1`
  - Notes: In `TaskRow.razor`, add `@inject PlanningService
    PlanningService` and `[Parameter] public EventCallback StatusChanged
    { get; set; }`. In the existing owner/status cell (alongside the
    current assignee/status text), add four buttons labeled exactly "Not
    started", "In progress", "Blocked", "Mark done" (matching legacy
    order — design.md Key Decision 2), each always visible/enabled
    regardless of `WorkItem.Status` (no disabling logic — spec.md NFR3).
    Each button's click handler: `var changedById = await
    PlanningService.GetCurrentManagerIdAsync(); await
    PlanningService.ChangeStatusAsync(WorkItem.Id, <status>, changedById);
    await StatusChanged.InvokeAsync();` — no `Reason` argument passed
    (spec.md NFR2/AC7), no confirmation dialog before the call (spec.md
    AC9).

    In `ProjectDetail.razor`, add `StatusChanged="RefreshAsync"` to both
    existing `<TaskRow WorkItem="task" />` usages (the per-objective loop
    and the Ungrouped section) — reuse the existing `RefreshAsync` method
    as-is, don't write a new lighter refresh (design.md Key Decision 4:
    this keeps the summary counts in sync with status changes, not just
    the row).

    Verify manually: `dotnet build` (AC1); through the running app, click
    a status button that differs from a task's current status and
    confirm via direct DB inspection that `WorkItem.Status` updated and
    exactly one new `StatusChange` row exists with the correct
    `FromStatus`/`ToStatus`/`ChangedById`/`Reason == null` (AC2); click
    "Mark done" and confirm `CompletedUtc` is set, then click a different
    status button and confirm `CompletedUtc` clears back to `null` (AC3);
    click the button matching a task's current status and confirm no new
    `StatusChange` row was created and `Status` is unchanged (AC4);
    confirm all four buttons render on rows in both the per-objective
    sections and the Ungrouped section (AC5); confirm the page's summary
    counts update after a status change without a manual "Refresh" click
    (AC6); confirm no `Reason` field or confirmation dialog exists
    anywhere in the rendered output (AC7, AC9). Use `form_input`/JS
    dispatch (`element.click()`) per `.specclaw/context.md`'s documented
    fallback if real mouse-click dispatch via claude-in-chrome is wedged
    again, as it has been for the last three changes running; fall back to
    a scratch console app calling `PlanningService` in-process against
    the live SQLite file if browser evidence alone is in doubt.

---

## Legend

- `[ ]` Pending
- `[~]` In Progress
- `[x]` Complete
- `[!]` Failed

**Task format:**
```
- [ ] `T<n>` — <title>
  - Files: <files to create/modify>
  - Estimate: small | medium | large
  - Depends: <task ids> (if any)
  - Notes: <additional context>
```
