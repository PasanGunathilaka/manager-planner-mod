namespace ManagerPlanner.Core.Domain;

/// <summary>Lifecycle of a project.</summary>
public enum ProjectStatus
{
    Active = 0,
    OnHold = 1,
    Completed = 2,
    Cancelled = 3
}

/// <summary>Lifecycle of a task.</summary>
public enum WorkItemStatus
{
    NotStarted = 0,
    InProgress = 1,
    Blocked = 2,
    Done = 3
}

/// <summary>How the manager met the team member.</summary>
public enum MeetingType
{
    VideoCall = 0,
    PhysicalMeeting = 1,
    PhoneCall = 2
}

/// <summary>Role of a person in the system.</summary>
public enum UserRole
{
    Manager = 0,
    TeamMember = 1
}
