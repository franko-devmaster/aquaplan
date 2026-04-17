using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplingContainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sampling_containers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sampling_id = table.Column<Guid>(type: "uuid", nullable: false),
                    container_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    barcode_scanned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sampling_containers", x => x.id);
                    table.ForeignKey(
                        name: "fk_sampling_containers_containers_container_id",
                        column: x => x.container_id,
                        principalTable: "containers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sampling_containers_samplings_sampling_id",
                        column: x => x.sampling_id,
                        principalTable: "samplings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sampling_containers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sampling_containers_container_id",
                table: "sampling_containers",
                column: "container_id");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_containers_sampling_id_container_id",
                table: "sampling_containers",
                columns: new[] { "sampling_id", "container_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sampling_containers_tenant_id_barcode",
                table: "sampling_containers",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");

            // Backfill sampling_containers from existing samplings.sample_barcode values.
            // For each sampling with a sample_barcode, pick the container of the first
            // analysis profile of the first analysis program of its order.
            // Samplings whose order has no analysis program (rare residual case) are skipped.
            migrationBuilder.Sql(@"
                INSERT INTO sampling_containers (id, sampling_id, container_id, barcode, barcode_scanned_at, tenant_id, created_at)
                SELECT gen_random_uuid(), s.id, chosen.container_id, s.sample_barcode, NULL, o.tenant_id, NOW() AT TIME ZONE 'UTC'
                FROM samplings s
                JOIN orders o ON o.id = s.order_id
                JOIN LATERAL (
                    SELECT ap.container_id
                    FROM order_analysis_programs oap
                    JOIN analysis_program_profiles app ON app.analysis_program_id = oap.analysis_program_id
                    JOIN analysis_profiles ap ON ap.id = app.analysis_profile_id
                    WHERE oap.order_id = s.order_id
                    ORDER BY ap.id
                    LIMIT 1
                ) AS chosen ON TRUE
                WHERE s.sample_barcode IS NOT NULL
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sampling_containers");
        }
    }
}
