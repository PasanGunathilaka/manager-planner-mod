# Proposal: Accountability reporting (promised-vs-delivered verdicts)

**Created:** 2026-08-03
**Status:** 🟡 Draft

## Problem

_What problem are we solving? Why does it matter?_

Nothing in the rebuild computes or shows a promised-vs-delivered verdict
yet. Per domain-model.md's DR-007, this is "the heart of the
accountability feature" the whole app exists for — cross-checking what a
team member promised (via `progress-notes-and-promise-tracking`'s
`ProgressNote.IsPromise`/`PromisedDate`) against what was actually
delivered (`WorkItem.Status`/`CompletedUtc`). Without this item, every
promise recorded by the previous change is captured but never surfaced
back to the Manager.

Rebuild-backlog item 8 merges both legacy apps' accountability surfaces —
Executive Planning Desktop's single-project Accountability tab and Manager
Planner Desktop's all-projects Accountability Report window — since both
are two different scopes of the same `Verdict` computation
(`GetAccountabilityReportAsync(projectId)` vs.
`GetAccountabilityForAllProjectsAsync()`).

Reading the real legacy source directly confirms the exact mechanics.
`../manager-planner/src/ExecutivePlanning.Core/Services/Reports.cs:1-67`
(the `AccountabilityRow` class):

```csharp
public class AccountabilityRow
{
    public int WorkItemId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = "(unassigned)";
    public WorkItemStatus Status { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? LatestPromisedDate { get; set; }
    public string? LatestPromiseText { get; set; }
    public DateTime? LatestPromiseRecordedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool IsOverdue { get; set; }
    public bool PromiseBroken { get; set; }
    public bool PromiseKept { get; set; }

    public string Verdict
    {
        get
        {
            if (PromiseKept) return "Kept promise";
            if (PromiseBroken) return "BROKE promise";
            if (IsOverdue) return "Overdue (no promise)";
            if (LatestPromisedDate.HasValue) return "Promise pending";
            return "On track";
        }
    }
}
```

`../manager-planner/src/ExecutivePlanning.Core/Services/PlanningService.cs:269-346`:

```csharp
public async Task<List<AccountabilityRow>> GetAccountabilityReportAsync(int projectId)
{
    var now = DateTime.UtcNow;
    var tasks = await _db.WorkItems
        .Include(t => t.Assignee).Include(t => t.Project).Include(t => t.Notes)
        .Where(t => t.ProjectId == projectId)
        .ToListAsync();

    var rows = new List<AccountabilityRow>();
    foreach (var t in tasks)
    {
        var latestPromise = t.Notes
            .Where(n => n.IsPromise && n.PromisedDate.HasValue)
            .OrderByDescending(n => n.CreatedUtc)
            .FirstOrDefault();

        var row = new AccountabilityRow { /* ...fields... */
            LatestPromisedDate = latestPromise?.PromisedDate,
            LatestPromiseText = latestPromise?.Text,
            LatestPromiseRecordedUtc = latestPromise?.CreatedUtc
        };

        row.IsOverdue = t.Deadline.HasValue && t.Deadline.Value < now && t.Status != WorkItemStatus.Done;

        if (latestPromise?.PromisedDate is DateTime promised)
        {
            if (t.Status == WorkItemStatus.Done)
            {
                row.PromiseKept = t.CompletedUtc.HasValue && t.CompletedUtc.Value.Date <= promised.Date;
                row.PromiseBroken = !row.PromiseKept;
            }
            else
            {
                row.PromiseBroken = promised.Date < now.Date;
            }
        }
        rows.Add(row);
    }

    return rows
        .OrderByDescending(r => r.PromiseBroken)
        .ThenByDescending(r => r.IsOverdue)
        .ThenBy(r => r.Deadline ?? DateTime.MaxValue)
        .ToList();
}

public async Task<List<AccountabilityRow>> GetAccountabilityForAllProjectsAsync()
{
    var rows = new List<AccountabilityRow>();
    var projectIds = await _db.Projects.OrderBy(p => p.Name).Select(p => p.Id).ToListAsync();
    foreach (var id in projectIds)
        rows.AddRange(await GetAccountabilityReportAsync(id));

    return rows
        .OrderByDescending(r => r.PromiseBroken)
        .ThenByDescending(r => r.IsOverdue)
        .ThenBy(r => r.ProjectName)
        .ThenBy(r => r.Deadline ?? DateTime.MaxValue)
        .ToList();
}
```

Two nuances this confirms, beyond domain-model.md's prose:

- **The all-projects sort inserts a `ProjectName` tie-break** between
  `IsOverdue` and `Deadline` that the single-project sort doesn't have
  (trivially, since it's already filtered to one project). Easy to miss
  porting from the single-project method alone.
- **`GetAccountabilityForAllProjectsAsync` is a thin wrapper** — it calls
  `GetAccountabilityReportAsync` once per project and re-sorts the
  concatenated rows; it is not a separate query/computation.

## The CQ-019/CQ-024 quirk — preserve, not fix

domain-model.md and rebuild-backlog.md both flag the same code-level
nuance as **"the single highest-priority verification input in this
backlog"**: `IsOverdue` is checked *before* `LatestPromisedDate.HasValue`
in the precedence chain, so a task whose deadline has passed but which
*does* carry a promise not yet due (not yet "broken") is labeled
**"Overdue (no promise)"** even though a promise is in fact on record.

This is exactly the kind of quirk `.specclaw/context.md` already warns
against "fixing": *"Don't silently 'fix' the accountability verdict
precedence or other quirks the analysis docs flag as intentional-looking
legacy behavior... ADR-0005 requires a golden-master capture and an
explicit product decision before deviating, not a 'senior-engineer
cleanup.'"*

The golden-master half of that requirement is **already satisfied** — the
baseline capture includes a fixture built specifically to catch this:
`.specclaw/baseline/fixtures/GM-010.json` (input `PromiseKept=false,
PromiseBroken=false, IsOverdue=true, latestPromisedDate="2099-01-01"` →
output `"Overdue (no promise)"`), with scenarios.md's own note: *"pinning
the legacy label text literally, even though `LatestPromisedDate.HasValue`
is true. This is the scenario a rebuild developer would be most tempted to
'fix' on sight; the fixture exists specifically to catch that."* The
explicit-product-decision half (`CQ-019` in decisions.md) is still
**unresolved** — see Open Questions below; this proposal recommends
preserving the quirk exactly, matching the captured fixture, but flags it
for your explicit sign-off rather than deciding it silently.

`CQ-024`'s "golden-master truth table for the Verdict computation and
tie-breaking sort order" is likewise already satisfied by 11 captured
scenarios, `GM-008` through `GM-018` — every precedence branch, both
exact-boundary conditions (`<=` for kept, strict `<` for broken), the
latest-promise-supersedes-an-earlier-one rule, and a genuine sort-tie
fixture (`GM-018`) recording the real legacy system's actual tie-break
order (no documented secondary key exists beyond the three stated ones —
the fixture pins whatever SQLite/EF Core's underlying stable order
actually produced, rather than inventing one).

## Proposed Solution

_What are we building? High-level approach._

1. **`Reports.cs` gains a new `AccountabilityRow` class**, ported field-
   for-field from the legacy source above (mutable class, not a record,
   matching this project's existing `ProjectSummary` DTO style in the same
   file).
2. **`PlanningService` gains two methods**, ported exactly:
   - `GetAccountabilityReportAsync(projectId)` — the full computation
     above, verbatim, including the flagged `IsOverdue`-before-promise
     precedence quirk.
   - `GetAccountabilityForAllProjectsAsync()` — the thin per-project
     wrapper + re-sort above, verbatim, including the extra `ProjectName`
     tie-break key.
3. **A new "Accountability" section on the existing `ProjectDetail.razor`**
   (the single-project scope, matching Executive Planning Desktop's tab):
   a read-only table — Task / Assignee / Status / Deadline / Promised /
   Verdict — most-at-risk sorted first, verdict text color-coded via
   MudBlazor's semantic `Color` enum (the same pattern already established
   for `TaskRow.StatusColor`): `Color.Success` (kept), `Color.Error`
   (broken), `Color.Warning` (overdue), `Color.Info` (pending),
   `Color.Default` (on track) — mapping directly from the legacy's own
   green/red/amber/blue/grey `VerdictBrush` scheme
   (`RowViewModels.cs:157-162`).
4. **A new page, `/accountability`**, for the all-projects scope (matching
   Manager Planner Desktop's dedicated window) — the **first genuinely new
   top-level route** this rebuild has needed, since every prior capability
   extended an existing page. `.specclaw/context.md` already anticipated
   this: *"Future backlog items (Accountability reporting, BL-008 — still
   not built) can add another `MudNavLink` here if they warrant a
   dedicated route."* Same read-only table shape as item 3, plus a
   `Project` column (since rows now span every project) — matching
   Manager Planner Desktop's window exactly, which "is read-only; no
   commands." A new `MudNavLink` is added to `MainLayout.razor`'s nav
   menu.
5. **`TaskRow.razor`'s `AddNoteAsync` gains a second, parameterless
   `NoteAdded` `EventCallback`**, wired to `ProjectDetail`'s existing full
   `RefreshAsync` — mirroring the legacy fidelity requirement quoted
   directly from functional-spec.md: *"the command also immediately
   recomputes the Accountability tab's rows"*
   (`MainWindowViewModel.cs:193-197` re-queries
   `GetAccountabilityReportAsync` right after saving a note).
   `progress-notes-and-promise-tracking` shipped `TaskRow`'s notes as
   local-state-only (no callback) because, at the time, nothing on the
   page derived from note state — this item changes that, since the new
   Accountability section's `Verdict`s are directly computed from notes.
   This follows the same reuse-the-existing-full-refresh shape already
   established for the status buttons' `StatusChanged` callback
   (`.specclaw/context.md`: *"Reuse the full refresh by default for this
   shape unless it's proven too expensive"*) — as a side effect, a status
   change now also correctly refreshes the Accountability section's
   verdicts, since they equally depend on `Status`/`CompletedUtc`.

## Scope

### In Scope
- `AccountabilityRow` class in `Reports.cs`
- `PlanningService.GetAccountabilityReportAsync(projectId)` and
  `GetAccountabilityForAllProjectsAsync()`, both verbatim ports
- A read-only Accountability table on `ProjectDetail.razor` (single-
  project scope)
- A new `/accountability` page (all-projects scope) + `MainLayout.razor`
  nav link
- `TaskRow.razor`'s `AddNoteAsync` raising a new `NoteAdded` callback,
  wired to `ProjectDetail.RefreshAsync`, so Accountability rows recompute
  immediately after a note is added (matching the legacy fidelity
  requirement quoted above)
- The `IsOverdue`-before-promise precedence quirk, preserved exactly

### Out of Scope
- **Any UI that lets a Manager act on a verdict** (e.g. re-open a
  conversation, dismiss a broken promise) — neither legacy window has any
  commands; both are explicitly read-only ("This window is read-only; no
  commands").
- **A "select project" filter on the new `/accountability` page** — the
  legacy Accountability Report window has none; it always shows every
  project.
- **A manual refresh button on `/accountability`** — no refresh
  affordance is documented for this window specifically (only page
  navigation re-fetches); the per-project section on `ProjectDetail.razor`
  keeps its existing page-level "Refresh" button, which already covers
  that scope.
- **A distinct "Owner" vs. "Assignee" column label between the two
  surfaces.** Manager Planner Desktop's window header reads "Owner," but
  both surfaces read the identical `AccountabilityRow.AssigneeName` field
  (single-assignee `WorkItem.Assignee`, not the v2 `TaskOwner` many-to-many
  join) — this proposal uses "Assignee" consistently on both surfaces
  rather than introducing a label difference with no behavioral meaning.
- **Fixing the `IsOverdue`-before-promise precedence quirk** — see the
  dedicated section above; this is preserved exactly pending your sign-off
  in Open Questions.
- **Any change to `progress-notes-and-promise-tracking`'s note-adding
  form** beyond the one new `EventCallback` invocation described above.

## Impact

- **Files affected:** ~5 (estimated) — `Reports.cs` (1 new class),
  `PlanningService.cs` (2 new methods), `ProjectDetail.razor` (new
  section + `NoteAdded` handler wiring), a new `Accountability.razor` page,
  `TaskRow.razor` (1 new `EventCallback` invocation), `MainLayout.razor`
  (1 new nav link)
- **Complexity:** medium — the computation itself is a direct, precisely-
  specified port with an existing 11-scenario golden-master truth table;
  the two-scope UI (a page section plus a genuinely new route) is more
  surface area than any prior item, but each piece is a repeat of an
  already-established pattern (read-only `MudSimpleTable`, semantic
  `Color` mapping, reuse-the-full-refresh callback shape)
- **Risk:** low for the computation itself (fully golden-mastered,
  GM-008–GM-018); the only real judgment call is the CQ-019 sign-off below

## Open Questions

1. **Preserve the "Overdue (no promise)" mislabeling exactly, as the
   captured golden master does, or fix it now?** `CQ-019` is recorded as
   an unresolved, blocking `DEFECT`-type question in decisions.md — this
   proposal's default, and the golden-master fixture's own stated intent,
   is to **preserve it exactly**: a task with an active, not-yet-due
   promise but a separately-passed deadline is labeled "Overdue (no
   promise)," not "Promise pending." This is a data-fidelity decision, not
   a UI one — reversing it later only requires reordering four `if`
   statements in `AccountabilityRow.Verdict`, so preserving it now costs
   nothing extra and keeps the rebuild honestly identical to the legacy
   system pending a real product decision. If you'd rather fix it now
   (i.e., check `LatestPromisedDate.HasValue` before `IsOverdue`), say so
   and I'll build the corrected order instead — and this becomes the
   `CQ-019` resolution, recorded as a decision rather than a default.
2. **Should the all-projects `/accountability` page get a "Refresh" button
   even though the legacy window has no commands at all?** Recommended:
   no, for literal fidelity (per Out of Scope above) — Blazor Server's own
   navigation/reconnect behavior already re-fetches on page load. Say so
   if you'd rather add one for convenience; it's a one-line addition
   either way.

---

**To proceed:** Review this proposal and approve to begin planning.
