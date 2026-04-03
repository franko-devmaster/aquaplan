using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "status_changed_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_changed_by",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "analysis_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    category = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_analysis_profiles_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_programs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_programs", x => x.id);
                    table.ForeignKey(
                        name: "fk_analysis_programs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_program_profiles",
                columns: table => new
                {
                    analysis_program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_profile_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_program_profiles", x => new { x.analysis_program_id, x.analysis_profile_id });
                    table.ForeignKey(
                        name: "fk_analysis_program_profiles_analysis_profiles_analysis_profil",
                        column: x => x.analysis_profile_id,
                        principalTable: "analysis_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_analysis_program_profiles_analysis_programs_analysis_progra",
                        column: x => x.analysis_program_id,
                        principalTable: "analysis_programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analysis_profiles_tenant_id_code",
                table: "analysis_profiles",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analysis_program_profiles_analysis_profile_id",
                table: "analysis_program_profiles",
                column: "analysis_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_analysis_programs_tenant_id_code",
                table: "analysis_programs",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_program_profiles");

            migrationBuilder.DropTable(
                name: "analysis_profiles");

            migrationBuilder.DropTable(
                name: "analysis_programs");

            migrationBuilder.DropColumn(
                name: "status_changed_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "status_changed_by",
                table: "orders");
        }
    }
}
