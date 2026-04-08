using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributorDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_delegated",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "distributor_delegations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegating_distributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegated_to_distributor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valid_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_distributor_delegations", x => x.id);
                    table.ForeignKey(
                        name: "fk_distributor_delegations_distributors_delegated_to_distribut",
                        column: x => x.delegated_to_distributor_id,
                        principalTable: "distributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_distributor_delegations_distributors_delegating_distributor",
                        column: x => x.delegating_distributor_id,
                        principalTable: "distributors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_distributor_delegations_delegated_to_distributor_id",
                table: "distributor_delegations",
                column: "delegated_to_distributor_id");

            migrationBuilder.CreateIndex(
                name: "ix_distributor_delegations_delegating_distributor_id_delegated",
                table: "distributor_delegations",
                columns: new[] { "delegating_distributor_id", "delegated_to_distributor_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "distributor_delegations");

            migrationBuilder.DropColumn(
                name: "is_delegated",
                table: "orders");
        }
    }
}
