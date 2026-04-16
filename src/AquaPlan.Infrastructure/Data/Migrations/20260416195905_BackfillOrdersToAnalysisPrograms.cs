using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOrdersToAnalysisPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Pre-script: for each analysis profile that is not yet attached to any program,
            //    create an auto-program "PROG-{profileCode}" within the same tenant.
            migrationBuilder.Sql(@"
                INSERT INTO analysis_programs (id, code, name, description, is_active, tenant_id, created_at)
                SELECT gen_random_uuid(),
                       'PROG-' || ap.code,
                       'Programme auto: ' || ap.name,
                       'Programme créé automatiquement pour le profil orphelin ' || ap.code,
                       true,
                       ap.tenant_id,
                       NOW() AT TIME ZONE 'UTC'
                FROM analysis_profiles ap
                WHERE NOT EXISTS (
                    SELECT 1 FROM analysis_program_profiles app
                    WHERE app.analysis_profile_id = ap.id
                );
            ");

            // 2. Attach every orphaned profile to its freshly created PROG-{code} program.
            migrationBuilder.Sql(@"
                INSERT INTO analysis_program_profiles (analysis_program_id, analysis_profile_id)
                SELECT apr.id, ap.id
                FROM analysis_profiles ap
                JOIN analysis_programs apr
                  ON apr.code = 'PROG-' || ap.code
                 AND apr.tenant_id = ap.tenant_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM analysis_program_profiles app
                    WHERE app.analysis_profile_id = ap.id
                );
            ");

            // 3. Create the new join table order_analysis_programs.
            migrationBuilder.CreateTable(
                name: "order_analysis_programs",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_program_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_analysis_programs", x => new { x.order_id, x.analysis_program_id });
                    table.ForeignKey(
                        name: "fk_order_analysis_programs_analysis_programs_analysis_program_",
                        column: x => x.analysis_program_id,
                        principalTable: "analysis_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_analysis_programs_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_analysis_programs_analysis_program_id",
                table: "order_analysis_programs",
                column: "analysis_program_id");

            // 4. Backfill: for each (order, profile) in the old table, insert DISTINCT (order, program) via analysis_program_profiles.
            migrationBuilder.Sql(@"
                INSERT INTO order_analysis_programs (order_id, analysis_program_id)
                SELECT DISTINCT oap.order_id, app.analysis_program_id
                FROM order_analysis_profiles oap
                JOIN analysis_program_profiles app
                  ON app.analysis_profile_id = oap.analysis_profile_id;
            ");

            // 5. Drop the legacy order_analysis_profiles table.
            migrationBuilder.DropTable(
                name: "order_analysis_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-create the legacy order_analysis_profiles table (empty).
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
                name: "ix_order_analysis_profiles_analysis_profile_id",
                table: "order_analysis_profiles",
                column: "analysis_profile_id");

            // Drop the new table. Note: the PROG-* auto-programs are intentionally not rolled back
            // since they may have been consumed by downstream data.
            migrationBuilder.DropTable(
                name: "order_analysis_programs");
        }
    }
}
