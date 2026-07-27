namespace ManagerPlanner.Core.Domain;

/// <summary>Immutable audit record of a task status transition. Gives the Manager a defensible history of when work actually moved forward (or stalled).</summary>
public class StatusChange
{
    public int Id { get; set; }
    public WorkItemStatus FromStatus { get; set; }
    public WorkItemStatus ToStatus { get; set; }
    public DateTime ChangedUtc { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }

    public int WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public int ChangedById { get; set; }
    public User ChangedBy { get; set; } = null!;
}
