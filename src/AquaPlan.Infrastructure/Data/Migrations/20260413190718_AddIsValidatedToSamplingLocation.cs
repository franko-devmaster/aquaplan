using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsValidatedToSamplingLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_validated",
                table: "sampling_locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_validated",
                table: "sampling_locations");
        }
    }
}
