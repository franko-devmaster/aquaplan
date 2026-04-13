using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributorIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "distributor_id",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            // Migrate data from UserDistributors join table to the new DistributorId column
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" u SET distributor_id = ud.distributor_id
                FROM user_distributors ud WHERE u.id = ud.user_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_distributor_id",
                table: "AspNetUsers",
                column: "distributor_id");

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_users_distributors_distributor_id",
                table: "AspNetUsers",
                column: "distributor_id",
                principalTable: "distributors",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_users_distributors_distributor_id",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_distributor_id",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "distributor_id",
                table: "AspNetUsers");
        }
    }
}
