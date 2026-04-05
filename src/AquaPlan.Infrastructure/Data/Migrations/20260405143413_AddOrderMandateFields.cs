using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderMandateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "planned_date",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sampling_location_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "order_analysis_profiles",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_profile_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_analysis_profiles", x => new { x.order_id, x.analysis_profile_id });
                    table.ForeignKey(
                        name: "fk_order_analysis_profiles_analysis_profiles_analysis_profile_",
                        column: x => x.analysis_profile_id,
                        principalTable: "analysis_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_analysis_profiles_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_sampling_location_id",
                table: "orders",
                column: "sampling_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_analysis_profiles_analysis_profile_id",
                table: "order_analysis_profiles",
                column: "analysis_profile_id");

            migrationBuilder.AddForeignKey(
                name: "fk_orders_sampling_locations_sampling_location_id",
                table: "orders",
                column: "sampling_location_id",
                principalTable: "sampling_locations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_orders_sampling_locations_sampling_location_id",
                table: "orders");

            migrationBuilder.DropTable(
                name: "order_analysis_profiles");

            migrationBuilder.DropIndex(
                name: "ix_orders_sampling_location_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "planned_date",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "sampling_location_id",
                table: "orders");
        }
    }
}
