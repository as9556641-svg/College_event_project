using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollegeEventRegistration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncementAndNotificationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionUrl",
                table: "Notifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "Notifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AudienceType = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    TargetRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EventId = table.Column<int>(type: "int", nullable: true),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Announcements_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Announcements_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CorrelationId",
                table: "Notifications",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_AudienceType",
                table: "Announcements",
                column: "AudienceType");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_AuthorUserId",
                table: "Announcements",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_DepartmentId",
                table: "Announcements",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_EventId",
                table: "Announcements",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_ExpiresAt",
                table: "Announcements",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Status",
                table: "Announcements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CorrelationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ActionUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Notifications");
        }
    }
}
