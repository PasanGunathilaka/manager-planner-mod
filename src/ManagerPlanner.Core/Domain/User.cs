namespace ManagerPlanner.Core.Domain;

/// <summary>A person in the system — either the Manager who plans, or a Team Member work is assigned to.</summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.TeamMember;
    public bool IsActive { get; set; } = true;

    public ICollection<Project> OwnedProjects { get; set; } = new List<Project>();
    public ICollection<WorkItem> AssignedTasks { get; set; } = new List<WorkItem>();
    public ICollection<TaskOwner> OwnedTasks { get; set; } = new List<TaskOwner>();
}
