using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairBank.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecuritySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowInternationalPayments",
                schema: "identity_service",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTwoFactorEnabled",
                schema: "identity_service",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NightTransactionsEnabled",
                schema: "identity_service",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequireApprovalAbove",
                schema: "identity_service",
                table: "users",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowInternationalPayments",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsTwoFactorEnabled",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "NightTransactionsEnabled",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RequireApprovalAbove",
                schema: "identity_service",
                table: "users");
        }
    }
}
