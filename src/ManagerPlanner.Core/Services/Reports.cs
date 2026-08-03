using ManagerPlanner.Core.Domain;

namespace ManagerPlanner.Core.Services;

/// <summary>
/// One row of the promised-vs-delivered accountability report.
/// Compares what a team member committed to (the latest promise note) against the
/// actual state of the task, so the Manager can hold the member to account next meeting.
/// </summary>
public class AccountabilityRow
{
    public int WorkItemId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = "(unassigned)";

    public WorkItemStatus Status { get; set; }
    public DateTime? Deadline { get; set; }

    /// <summary>The most recent promise the member made against this task, if any.</summary>
    public DateTime? LatestPromisedDate { get; set; }
    public string? LatestPromiseText { get; set; }
    public DateTime? LatestPromiseRecordedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    // Derived verdicts ---------------------------------------------------
    /// <summary>Deadline in the past and not Done.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>Member promised a date, that date has passed, and the task is still not Done.</summary>
    public bool PromiseBroken { get; set; }

    /// <summary>Member made a promise and delivered (Done) on or before the promised date.</summary>
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

/// <summary>Computed per-project task-count summary — not EF-mapped, built fresh on every call.</summary>
public class ProjectSummary
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int Done { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int NotStarted { get; set; }
    public int Overdue { get; set; }
    public int Discovered { get; set; }

    public double PercentComplete => TotalTasks == 0 ? 0 : Math.Round(100.0 * Done / TotalTasks, 1);
}
