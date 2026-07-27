namespace ManagerPlanner.Core.Domain;

/// <summary>Join entity for the many-to-many between tasks and their owners, so a task can be owned by several people. Kept explicit so ownership can carry data later and to keep the relational model obvious.</summary>
public class TaskOwner
{
    public int WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
