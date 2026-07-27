using ManagerPlanner.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ManagerPlanner.Core.Data;

/// <summary>
/// EF Core context for the Manager Planner database.
/// Configures the relational schema, foreign keys, indexes and delete behaviours.
/// </summary>
public class PlanningDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Objective> Objectives => Set<Objective>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<TaskOwner> TaskOwners => Set<TaskOwner>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<ProgressNote> ProgressNotes => Set<ProgressNote>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();

    public PlanningDbContext(DbContextOptions<PlanningDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---- User ------------------------------------------------------
        b.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            e.Property(u => u.Email).HasMaxLength(200);
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ---- Project ---------------------------------------------------
        b.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(p => p.Name);

            // Project.Owner (Manager) 1 ---- * Project
            // Restrict: you cannot delete a user who still owns projects.
            e.HasOne(p => p.Owner)
             .WithMany(u => u.OwnedProjects)
             .HasForeignKey(p => p.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- WorkItem (Task) --------------------------------------------
        b.Entity<WorkItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(250);
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.Deadline);

            // Project 1 ---- * WorkItem : deleting a project removes its tasks.
            e.HasOne(t => t.Project)
             .WithMany(p => p.Tasks)
             .HasForeignKey(t => t.ProjectId)
             .OnDelete(DeleteBehavior.Cascade);

            // Assignee (User) 1 ---- * WorkItem : keep tasks if the user is removed.
            e.HasOne(t => t.Assignee)
             .WithMany(u => u.AssignedTasks)
             .HasForeignKey(t => t.AssigneeId)
             .OnDelete(DeleteBehavior.SetNull);

            // Meeting where the task was discovered (optional).
            e.HasOne(t => t.DiscoveredInMeeting)
             .WithMany(m => m.DiscoveredTasks)
             .HasForeignKey(t => t.DiscoveredInMeetingId)
             .OnDelete(DeleteBehavior.SetNull);

            // Objective 1 ---- * WorkItem (optional grouping).
            e.HasOne(t => t.Objective)
             .WithMany(o => o.Tasks)
             .HasForeignKey(t => t.ObjectiveId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- Objective ---------------------------------------------------
        b.Entity<Objective>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Title).IsRequired().HasMaxLength(250);

            e.HasOne(o => o.Project)
             .WithMany(p => p.Objectives)
             .HasForeignKey(o => o.ProjectId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- ChecklistItem (nested tree) ---------------------------------
        b.Entity<ChecklistItem>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Label).IsRequired().HasMaxLength(300);

            // Task 1 ---- * ChecklistItem : items die with the task.
            e.HasOne(c => c.WorkItem)
             .WithMany(t => t.Checklist)
             .HasForeignKey(c => c.WorkItemId)
             .OnDelete(DeleteBehavior.Cascade);

            // Self-reference for nesting. Restrict (children removed in app code)
            // to avoid multiple cascade paths on SQLite.
            e.HasOne(c => c.Parent)
             .WithMany(c => c.Children)
             .HasForeignKey(c => c.ParentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Assignee)
             .WithMany()
             .HasForeignKey(c => c.AssigneeId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- TaskOwner (many-to-many WorkItem <-> User) ------------------
        b.Entity<TaskOwner>(e =>
        {
            e.HasKey(x => new { x.WorkItemId, x.UserId });

            e.HasOne(x => x.WorkItem)
             .WithMany(t => t.Owners)
             .HasForeignKey(x => x.WorkItemId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
             .WithMany(u => u.OwnedTasks)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Meeting -------------------------------------------------------
        b.Entity<Meeting>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Title).IsRequired().HasMaxLength(250);
            e.HasIndex(m => m.MeetingDate);

            e.HasOne(m => m.Project)
             .WithMany(p => p.Meetings)
             .HasForeignKey(m => m.ProjectId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Participant)
             .WithMany()
             .HasForeignKey(m => m.ParticipantId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- ProgressNote ----------------------------------------------
        b.Entity<ProgressNote>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Text).IsRequired();
            e.HasIndex(n => n.CreatedUtc);

            // WorkItem 1 ---- * ProgressNote : notes die with the task.
            e.HasOne(n => n.WorkItem)
             .WithMany(t => t.Notes)
             .HasForeignKey(n => n.WorkItemId)
             .OnDelete(DeleteBehavior.Cascade);

            // Meeting 1 ---- * ProgressNote (optional). Keep the note if meeting removed.
            e.HasOne(n => n.Meeting)
             .WithMany(m => m.Notes)
             .HasForeignKey(n => n.MeetingId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(n => n.Author)
             .WithMany()
             .HasForeignKey(n => n.AuthorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StatusChange ----------------------------------------------
        b.Entity<StatusChange>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.ChangedUtc);

            e.HasOne(s => s.WorkItem)
             .WithMany(t => t.StatusHistory)
             .HasForeignKey(s => s.WorkItemId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.ChangedBy)
             .WithMany()
             .HasForeignKey(s => s.ChangedById)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
