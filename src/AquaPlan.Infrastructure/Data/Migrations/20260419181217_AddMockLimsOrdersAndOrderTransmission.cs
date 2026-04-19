using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMockLimsOrdersAndOrderTransmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lims_order_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "transmitted_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mock_lims_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lims_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sampling_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    results_json = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    results_ready_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mock_lims_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mock_lims_orders_lims_order_id",
                table: "mock_lims_orders",
                column: "lims_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mock_lims_orders_tenant_id_order_reference",
                table: "mock_lims_orders",
                columns: new[] { "tenant_id", "order_reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mock_lims_orders");

            migrationBuilder.DropColumn(
                name: "lims_order_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "transmitted_at",
                table: "orders");
        }
    }
}
