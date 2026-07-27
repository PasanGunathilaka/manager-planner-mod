namespace ManagerPlanner.Core.Domain;

/// <summary>A recorded conversation (video/physical/phone) between the Manager and a team member. Notes captured during the meeting hang off this record.</summary>
public class Meeting
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public MeetingType Type { get; set; } = MeetingType.VideoCall;
    public DateTime MeetingDate { get; set; } = DateTime.UtcNow;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int? ParticipantId { get; set; }
    public User? Participant { get; set; }

    public ICollection<ProgressNote> Notes { get; set; } = new List<ProgressNote>();
    public ICollection<WorkItem> DiscoveredTasks { get; set; } = new List<WorkItem>();
}
