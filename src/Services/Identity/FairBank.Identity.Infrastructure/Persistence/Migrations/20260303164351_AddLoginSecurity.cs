using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairBank.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveSessionId",
                schema: "identity_service",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                schema: "identity_service",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                schema: "identity_service",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveSessionId",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                schema: "identity_service",
                table: "users");
        }
    }
}
