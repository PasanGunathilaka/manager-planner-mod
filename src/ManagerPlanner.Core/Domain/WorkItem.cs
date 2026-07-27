namespace ManagerPlanner.Core.Domain;

/// <summary>A unit of work under a project, assigned to a team member with a deadline. A task can be "discovered" during a meeting — in which case DiscoveredInMeetingId is set.</summary>
public class WorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.NotStarted;
    public DateTime? Deadline { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public bool IsDiscovered { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int? ObjectiveId { get; set; }
    public Objective? Objective { get; set; }

    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public int? DiscoveredInMeetingId { get; set; }
    public Meeting? DiscoveredInMeeting { get; set; }

    public ICollection<ProgressNote> Notes { get; set; } = new List<ProgressNote>();
    public ICollection<StatusChange> StatusHistory { get; set; } = new List<StatusChange>();
    public ICollection<ChecklistItem> Checklist { get; set; } = new List<ChecklistItem>();
    public ICollection<TaskOwner> Owners { get; set; } = new List<TaskOwner>();
}
