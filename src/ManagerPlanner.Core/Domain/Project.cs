namespace ManagerPlanner.Core.Domain;

/// <summary>A body of work the Manager plans and tracks. Owns many tasks and meetings.</summary>
public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public ICollection<Objective> Objectives { get; set; } = new List<Objective>();
    public ICollection<WorkItem> Tasks { get; set; } = new List<WorkItem>();
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
}
