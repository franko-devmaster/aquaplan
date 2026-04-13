using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributorIdToSector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "distributor_id",
                table: "sectors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_sectors_distributor_id",
                table: "sectors",
                column: "distributor_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sectors_distributors_distributor_id",
                table: "sectors",
                column: "distributor_id",
                principalTable: "distributors",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sectors_distributors_distributor_id",
                table: "sectors");

            migrationBuilder.DropIndex(
                name: "ix_sectors_distributor_id",
                table: "sectors");

            migrationBuilder.DropColumn(
                name: "distributor_id",
                table: "sectors");
        }
    }
}
