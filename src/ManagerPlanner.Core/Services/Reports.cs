namespace ManagerPlanner.Core.Services;

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
