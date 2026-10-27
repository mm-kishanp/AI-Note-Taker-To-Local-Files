using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GranolaPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ClassificationReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeetingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncLeases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLeases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NotesDiscovered = table.Column<int>(type: "INTEGER", nullable: false),
                    MeetingsProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    NotesExported = table.Column<int>(type: "INTEGER", nullable: false),
                    TranscriptsExported = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    MeetingsSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassificationFailures = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiFailures = table.Column<int>(type: "INTEGER", nullable: false),
                    FileSystemFailures = table.Column<int>(type: "INTEGER", nullable: false),
                    Conflicts = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAfterFilter = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NotesFolderPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    TranscriptFolderPath = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    DefaultClassificationPriority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SyncRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeetingId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GranolaNoteId = table.Column<string>(type: "TEXT", nullable: true),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ErrorCategory = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Retryable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRunItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncRunItems_SyncRuns_SyncRunId",
                        column: x => x.SyncRunId,
                        principalTable: "SyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    NormalizedValue = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationRules", x => x.Id);
                    table.CheckConstraint("CK_Rule_Owner", "(\"ProjectId\" IS NULL) <> (\"ClientId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ClassificationRules_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassificationRules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GranolaNoteId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MeetingStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MeetingEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerName = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerEmail = table.Column<string>(type: "TEXT", nullable: true),
                    AttendeesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParticipantEmailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SpeakerMetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CalendarEventJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFoldersJson = table.Column<string>(type: "TEXT", nullable: false),
                    AttachmentsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    NotesContent = table.Column<string>(type: "TEXT", nullable: true),
                    TranscriptContent = table.Column<string>(type: "TEXT", nullable: true),
                    TranscriptSegmentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SourceFolderId = table.Column<string>(type: "TEXT", nullable: true),
                    NotesStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TranscriptStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SourceStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AssignedProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClassificationStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AssignmentMethod = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Certainty = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ClassificationReason = table.Column<string>(type: "TEXT", nullable: true),
                    ClassificationDetailJson = table.Column<string>(type: "TEXT", nullable: false),
                    NeedsReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    ManualAssignment = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ManualProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ManualAssignedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExportStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ExportPending = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotesExportPath = table.Column<string>(type: "TEXT", nullable: true),
                    TranscriptExportPath = table.Column<string>(type: "TEXT", nullable: true),
                    SourceContentHash = table.Column<string>(type: "TEXT", nullable: true),
                    LastSuccessfulFetchAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSuccessfulExportAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncError = table.Column<string>(type: "TEXT", nullable: true),
                    LastErrorCategory = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedLocallyAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedLocallyAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Projects_AssignedProjectId",
                        column: x => x.AssignedProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ExportRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeetingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    LastGeneratedHash = table.Column<string>(type: "TEXT", nullable: true),
                    SourceContentHash = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    LastExportedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConflictDetectedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportRecords_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeetingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAttendee = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsInvitee = table.Column<bool>(type: "INTEGER", nullable: false),
                    SpokenSegments = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingParticipants_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingProjectAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeetingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingProjectAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingProjectAssignments_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingProjectAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationReviews_MeetingId",
                table: "ClassificationReviews",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationRules_ClientId",
                table: "ClassificationRules",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationRules_Kind_NormalizedValue",
                table: "ClassificationRules",
                columns: new[] { "Kind", "NormalizedValue" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassificationRules_ProjectId",
                table: "ClassificationRules",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportRecords_MeetingId_Kind_Location_ProjectId",
                table: "ExportRecords",
                columns: new[] { "MeetingId", "Kind", "Location", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportRecords_ProjectId",
                table: "ExportRecords",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportRecords_RelativePath",
                table: "ExportRecords",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_ExportRecords_Status",
                table: "ExportRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_Email",
                table: "MeetingParticipants",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_MeetingId",
                table: "MeetingParticipants",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingProjectAssignments_MeetingId_ProjectId",
                table: "MeetingProjectAssignments",
                columns: new[] { "MeetingId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingProjectAssignments_ProjectId",
                table: "MeetingProjectAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_AssignedProjectId",
                table: "Meetings",
                column: "AssignedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ClassificationStatus",
                table: "Meetings",
                column: "ClassificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CreatedAt",
                table: "Meetings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ExportPending_NextRetryAt",
                table: "Meetings",
                columns: new[] { "ExportPending", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ExportStatus",
                table: "Meetings",
                column: "ExportStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_GranolaNoteId",
                table: "Meetings",
                column: "GranolaNoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MeetingStartTime",
                table: "Meetings",
                column: "MeetingStartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ClientId",
                table: "Projects",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Slug",
                table: "Projects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRunItems_MeetingId",
                table: "SyncRunItems",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRunItems_SyncRunId",
                table: "SyncRunItems",
                column: "SyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_StartedAt",
                table: "SyncRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_Status",
                table: "SyncRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "ClassificationReviews");

            migrationBuilder.DropTable(
                name: "ClassificationRules");

            migrationBuilder.DropTable(
                name: "ExportRecords");

            migrationBuilder.DropTable(
                name: "MeetingParticipants");

            migrationBuilder.DropTable(
                name: "MeetingProjectAssignments");

            migrationBuilder.DropTable(
                name: "SyncLeases");

            migrationBuilder.DropTable(
                name: "SyncRunItems");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "SyncRuns");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
