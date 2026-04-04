using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplingLocationChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sampling_location_change_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    sampling_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    distributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    proposed_location_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    proposed_latitude = table.Column<double>(type: "double precision", nullable: true),
                    proposed_longitude = table.Column<double>(type: "double precision", nullable: true),
                    proposed_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    requested_by_id = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_id = table.Column<string>(type: "text", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sampling_location_change_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_sampling_location_change_requests_distributors_distributor_",
                        column: x => x.distributor_id,
                        principalTable: "distributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_location_change_requests_sampling_locations_sampli",
                        column: x => x.sampling_location_id,
                        principalTable: "sampling_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sampling_location_change_requests_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sampling_location_change_requests_users_requested_by_id",
                        column: x => x.requested_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_location_change_requests_users_reviewed_by_id",
                        column: x => x.reviewed_by_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sampling_location_change_requests_distributor_id",
                table: "sampling_location_change_requests",
                column: "distributor_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_location_change_requests_requested_by_id",
                table: "sampling_location_change_requests",
                column: "requested_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_location_change_requests_reviewed_by_id",
                table: "sampling_location_change_requests",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_location_change_requests_sampling_location_id",
                table: "sampling_location_change_requests",
                column: "sampling_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_location_change_requests_tenant_id_status",
                table: "sampling_location_change_requests",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sampling_location_change_requests");
        }
    }
}
