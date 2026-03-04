using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairBank.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AgreedToTermsAt",
                schema: "identity_service",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "identity_service",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "identity_service",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "identity_service",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationToken",
                schema: "identity_service",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationTokenExpiresAt",
                schema: "identity_service",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                schema: "identity_service",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                schema: "identity_service",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiresAt",
                schema: "identity_service",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalIdNumber",
                schema: "identity_service",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "identity_service",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                schema: "identity_service",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                schema: "identity_service",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "identity_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_CreatedAt",
                schema: "identity_service",
                table: "notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_IsRead",
                schema: "identity_service",
                table: "notifications",
                columns: new[] { "UserId", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications",
                schema: "identity_service");

            migrationBuilder.DropColumn(
                name: "AgreedToTermsAt",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationToken",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenExpiresAt",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiresAt",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PersonalIdNumber",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Street",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                schema: "identity_service",
                table: "users");
        }
    }
}
