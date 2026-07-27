using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagerPlanner.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    MeetingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Meetings_Users_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Objectives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    KeyResult = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Objectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Objectives_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Deadline = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDiscovered = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ObjectiveId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssigneeId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscoveredInMeetingId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItems_Meetings_DiscoveredInMeetingId",
                        column: x => x.DiscoveredInMeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkItems_Objectives_ObjectiveId",
                        column: x => x.ObjectiveId,
                        principalTable: "Objectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItems_Users_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssigneeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_ChecklistItems_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Users_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgressNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NoteDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPromise = table.Column<bool>(type: "INTEGER", nullable: false),
                    PromisedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: true),
                    AuthorId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressNotes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProgressNotes_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgressNotes_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatusChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedById = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusChanges_Users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatusChanges_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskOwners",
                columns: table => new
                {
                    WorkItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskOwners", x => new { x.WorkItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TaskOwners_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskOwners_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_AssigneeId",
                table: "ChecklistItems",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_ParentId",
                table: "ChecklistItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_WorkItemId",
                table: "ChecklistItems",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MeetingDate",
                table: "Meetings",
                column: "MeetingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ParticipantId",
                table: "Meetings",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ProjectId",
                table: "Meetings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Objectives_ProjectId",
                table: "Objectives",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressNotes_AuthorId",
                table: "ProgressNotes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressNotes_CreatedUtc",
                table: "ProgressNotes",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressNotes_MeetingId",
                table: "ProgressNotes",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressNotes_WorkItemId",
                table: "ProgressNotes",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusChanges_ChangedById",
                table: "StatusChanges",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_StatusChanges_ChangedUtc",
                table: "StatusChanges",
                column: "ChangedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StatusChanges_WorkItemId",
                table: "StatusChanges",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskOwners_UserId",
                table: "TaskOwners",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssigneeId",
                table: "WorkItems",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Deadline",
                table: "WorkItems",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_DiscoveredInMeetingId",
                table: "WorkItems",
                column: "DiscoveredInMeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ObjectiveId",
                table: "WorkItems",
                column: "ObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId",
                table: "WorkItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Status",
                table: "WorkItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "ProgressNotes");

            migrationBuilder.DropTable(
                name: "StatusChanges");

            migrationBuilder.DropTable(
                name: "TaskOwners");

            migrationBuilder.DropTable(
                name: "WorkItems");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Objectives");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
