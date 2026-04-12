using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplingRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "location_replacement_reason",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "original_sampling_location_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sampler_comment",
                table: "orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sampling_round_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "sampling_rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    preleveur_id = table.Column<string>(type: "text", nullable: true),
                    distributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sampling_rounds", x => x.id);
                    table.ForeignKey(
                        name: "fk_sampling_rounds_distributors_distributor_id",
                        column: x => x.distributor_id,
                        principalTable: "distributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_rounds_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_rounds_users_preleveur_id",
                        column: x => x.preleveur_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_original_sampling_location_id",
                table: "orders",
                column: "original_sampling_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_sampling_round_id_sort_order",
                table: "orders",
                columns: new[] { "sampling_round_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_sampling_rounds_created_by_id",
                table: "sampling_rounds",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_rounds_distributor_id_preleveur_id",
                table: "sampling_rounds",
                columns: new[] { "distributor_id", "preleveur_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sampling_rounds_preleveur_id",
                table: "sampling_rounds",
                column: "preleveur_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_rounds_tenant_id",
                table: "sampling_rounds",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_rounds_tenant_id_deadline",
                table: "sampling_rounds",
                columns: new[] { "tenant_id", "deadline" });

            migrationBuilder.AddForeignKey(
                name: "fk_orders_sampling_locations_original_sampling_location_id",
                table: "orders",
                column: "original_sampling_location_id",
                principalTable: "sampling_locations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_sampling_rounds_sampling_round_id",
                table: "orders",
                column: "sampling_round_id",
                principalTable: "sampling_rounds",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_orders_sampling_locations_original_sampling_location_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_sampling_rounds_sampling_round_id",
                table: "orders");

            migrationBuilder.DropTable(
                name: "sampling_rounds");

            migrationBuilder.DropIndex(
                name: "ix_orders_original_sampling_location_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_sampling_round_id_sort_order",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "location_replacement_reason",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "original_sampling_location_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "sampler_comment",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "sampling_round_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "orders");
        }
    }
}
