using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplingPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sampling_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    distributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    status_changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status_changed_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sampling_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_sampling_plans_distributors_distributor_id",
                        column: x => x.distributor_id,
                        principalTable: "distributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_plans_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sampling_plan_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sampling_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sampling_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency_per_year = table.Column<int>(type: "integer", nullable: false),
                    planned_months = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sampling_plan_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_sampling_plan_items_analysis_profiles_analysis_profile_id",
                        column: x => x.analysis_profile_id,
                        principalTable: "analysis_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_plan_items_sampling_locations_sampling_location_id",
                        column: x => x.sampling_location_id,
                        principalTable: "sampling_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_plan_items_sampling_plans_sampling_plan_id",
                        column: x => x.sampling_plan_id,
                        principalTable: "sampling_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sampling_plan_items_analysis_profile_id",
                table: "sampling_plan_items",
                column: "analysis_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_plan_items_sampling_location_id",
                table: "sampling_plan_items",
                column: "sampling_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_plan_items_sampling_plan_id",
                table: "sampling_plan_items",
                column: "sampling_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_plans_created_by_id",
                table: "sampling_plans",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_plans_distributor_id_year",
                table: "sampling_plans",
                columns: new[] { "distributor_id", "year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sampling_plan_items");

            migrationBuilder.DropTable(
                name: "sampling_plans");
        }
    }
}
