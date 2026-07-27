namespace ManagerPlanner.Core.Domain;

/// <summary>A nested progress item under a task — the "checklist" column in the planner grid. Items form a tree via ParentId, each individually tickable and optionally owned by a person.</summary>
public class ChecklistItem
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CompletedUtc { get; set; }

    public int WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public int? ParentId { get; set; }
    public ChecklistItem? Parent { get; set; }
    public ICollection<ChecklistItem> Children { get; set; } = new List<ChecklistItem>();

    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }
}
