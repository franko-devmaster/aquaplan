using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundLockFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_locked",
                table: "sampling_rounds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_at",
                table: "sampling_rounds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "locked_by_id",
                table: "sampling_rounds",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sampling_rounds_locked_by_id",
                table: "sampling_rounds",
                column: "locked_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sampling_rounds_users_locked_by_id",
                table: "sampling_rounds",
                column: "locked_by_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sampling_rounds_users_locked_by_id",
                table: "sampling_rounds");

            migrationBuilder.DropIndex(
                name: "ix_sampling_rounds_locked_by_id",
                table: "sampling_rounds");

            migrationBuilder.DropColumn(
                name: "is_locked",
                table: "sampling_rounds");

            migrationBuilder.DropColumn(
                name: "locked_at",
                table: "sampling_rounds");

            migrationBuilder.DropColumn(
                name: "locked_by_id",
                table: "sampling_rounds");
        }
    }
}
