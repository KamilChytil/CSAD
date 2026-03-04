using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairBank.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorAuthAndUserDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "two_factor_auth",
                schema: "identity_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BackupCodes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_two_factor_auth", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_devices",
                schema: "identity_service",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Browser = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OperatingSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsTrusted = table.Column<bool>(type: "boolean", nullable: false),
                    IsCurrentDevice = table.Column<bool>(type: "boolean", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_devices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_auth_UserId",
                schema: "identity_service",
                table: "two_factor_auth",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_devices_UserId",
                schema: "identity_service",
                table: "user_devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_fingerprint",
                schema: "identity_service",
                table: "user_devices",
                columns: new[] { "UserId", "Browser", "OperatingSystem", "DeviceType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "two_factor_auth",
                schema: "identity_service");

            migrationBuilder.DropTable(
                name: "user_devices",
                schema: "identity_service");
        }
    }
}
