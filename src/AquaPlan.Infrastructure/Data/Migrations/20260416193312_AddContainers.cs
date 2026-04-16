using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the containers table
            migrationBuilder.CreateTable(
                name: "containers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    material = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    volume_ml = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_containers", x => x.id);
                    table.ForeignKey(
                        name: "fk_containers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_containers_tenant_id_code",
                table: "containers",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            // 2. Seed the 6 default containers for every existing tenant
            migrationBuilder.Sql(@"
                INSERT INTO containers (id, code, name, material, volume_ml, color, is_active, tenant_id, created_at)
                SELECT gen_random_uuid(), seed.code, seed.name, seed.material, seed.volume_ml, seed.color, true, t.id, NOW() AT TIME ZONE 'UTC'
                FROM tenants t
                CROSS JOIN (VALUES
                    ('BACT-V250', 'Bouteille verre stérile microbiologie', 'Verre borosilicaté', 250, 'Transparent'),
                    ('CHEM-PET500', 'Bouteille PET chimie', 'PET', 500, 'Transparent'),
                    ('CHEM-PEHD250', 'Flacon PEHD traces organiques', 'PEHD', 250, 'Blanc opaque'),
                    ('PHY-V100', 'Flacon verre paramètres physiques', 'Verre', 100, 'Ambré'),
                    ('PEST-V1000', 'Bouteille verre ambré pesticides/micropolluants', 'Verre ambré', 1000, 'Ambré'),
                    ('ISOT-V60', 'Flacon verre isotopes/COV (septum)', 'Verre', 60, 'Transparent')
                ) AS seed(code, name, material, volume_ml, color);
            ");

            // 3. Add container_id column as nullable to allow backfill
            migrationBuilder.AddColumn<Guid>(
                name: "container_id",
                table: "analysis_profiles",
                type: "uuid",
                nullable: true);

            // 4. Backfill container_id by analysis profile category (per tenant)
            //    Bacteriology -> BACT-V250
            //    Chemistry    -> CHEM-PET500
            //    Physical     -> PHY-V100
            //    Other        -> CHEM-PET500
            migrationBuilder.Sql(@"
                UPDATE analysis_profiles ap
                SET container_id = c.id
                FROM containers c
                WHERE c.tenant_id = ap.tenant_id
                  AND c.code = CASE ap.category
                        WHEN 0 THEN 'BACT-V250'
                        WHEN 1 THEN 'CHEM-PET500'
                        WHEN 2 THEN 'PHY-V100'
                        ELSE 'CHEM-PET500'
                    END;
            ");

            // 5. Enforce NOT NULL now that every row has a value
            migrationBuilder.AlterColumn<Guid>(
                name: "container_id",
                table: "analysis_profiles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 6. Index + FK (Restrict to prevent deleting a container used by a profile)
            migrationBuilder.CreateIndex(
                name: "ix_analysis_profiles_container_id",
                table: "analysis_profiles",
                column: "container_id");

            migrationBuilder.AddForeignKey(
                name: "fk_analysis_profiles_containers_container_id",
                table: "analysis_profiles",
                column: "container_id",
                principalTable: "containers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_analysis_profiles_containers_container_id",
                table: "analysis_profiles");

            migrationBuilder.DropIndex(
                name: "ix_analysis_profiles_container_id",
                table: "analysis_profiles");

            migrationBuilder.DropColumn(
                name: "container_id",
                table: "analysis_profiles");

            migrationBuilder.DropTable(
                name: "containers");
        }
    }
}
