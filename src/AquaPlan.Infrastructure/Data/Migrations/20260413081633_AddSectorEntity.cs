using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSectorEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sector_id",
                table: "sampling_locations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sectors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sectors", x => x.id);
                    table.ForeignKey(
                        name: "fk_sectors_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sampling_locations_sector_id",
                table: "sampling_locations",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_sectors_code_tenant_id",
                table: "sectors",
                columns: new[] { "code", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sectors_tenant_id",
                table: "sectors",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_sampling_locations_sectors_sector_id",
                table: "sampling_locations",
                column: "sector_id",
                principalTable: "sectors",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_sampling_locations_sectors_sector_id",
                table: "sampling_locations");

            migrationBuilder.DropTable(
                name: "sectors");

            migrationBuilder.DropIndex(
                name: "ix_sampling_locations_sector_id",
                table: "sampling_locations");

            migrationBuilder.DropColumn(
                name: "sector_id",
                table: "sampling_locations");
        }
    }
}
