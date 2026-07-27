namespace ManagerPlanner.Core.Domain;

/// <summary>A goal within a project. Sits between Project and WorkItem so work is grouped the way a manager plans it: Project → Objective → Task.</summary>
public class Objective
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? KeyResult { get; set; }
    public int SortOrder { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public ICollection<WorkItem> Tasks { get; set; } = new List<WorkItem>();
}
