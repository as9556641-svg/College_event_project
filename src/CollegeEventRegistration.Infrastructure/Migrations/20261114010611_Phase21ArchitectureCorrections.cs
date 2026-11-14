using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollegeEventRegistration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase21ArchitectureCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_RegistrationId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId",
                table: "Payments",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_Registration_Success",
                table: "Payments",
                column: "RegistrationId",
                unique: true,
                filter: "[Status] = 3");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_TransactionId",
                table: "Payments",
                column: "TransactionId",
                unique: true,
                filter: "[TransactionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_Capacity",
                table: "Events",
                sql: "[RegisteredCount] <= [TotalCapacity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_NonNegativeCounts",
                table: "Events",
                sql: "[RegisteredCount] >= 0 AND [TotalCapacity] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RegistrationId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "UX_Payments_Registration_Success",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "UX_Payments_TransactionId",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_Capacity",
                table: "Events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_NonNegativeCounts",
                table: "Events");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId",
                table: "Payments",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments",
                column: "TransactionId");
        }
    }
}
