using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairBank.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentChildRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                schema: "identity_service",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_ParentId",
                schema: "identity_service",
                table: "users",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_ParentId",
                schema: "identity_service",
                table: "users",
                column: "ParentId",
                principalSchema: "identity_service",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_users_ParentId",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_ParentId",
                schema: "identity_service",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "identity_service",
                table: "users");
        }
    }
}
