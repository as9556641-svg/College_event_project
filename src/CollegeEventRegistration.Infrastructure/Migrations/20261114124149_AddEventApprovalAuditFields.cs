using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollegeEventRegistration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventApprovalAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Events",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "Events",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Events",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_ApprovedByUserId",
                table: "Events",
                column: "ApprovedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_AspNetUsers_ApprovedByUserId",
                table: "Events",
                column: "ApprovedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_AspNetUsers_ApprovedByUserId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_ApprovedByUserId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Events");
        }
    }
}
