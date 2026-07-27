namespace ManagerPlanner.Core.Domain;

/// <summary>A note the Manager records against a task — typically during a meeting — capturing what the team member said. The Manager can flag that the team member promised something by a certain date, then later cross-check promise vs delivery.</summary>
public class ProgressNote
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The date the note is *about*, distinct from when it was typed.</summary>
    public DateTime NoteDate { get; set; } = DateTime.UtcNow;

    public bool IsPromise { get; set; }
    public DateTime? PromisedDate { get; set; }

    public int WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public int? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
}
