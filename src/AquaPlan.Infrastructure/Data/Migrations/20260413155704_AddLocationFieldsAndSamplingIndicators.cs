using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationFieldsAndSamplingIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_water_softener",
                table: "samplings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_chlorinated",
                table: "samplings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "access_description",
                table: "sampling_locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "sampling_locations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_water_softener",
                table: "samplings");

            migrationBuilder.DropColumn(
                name: "is_chlorinated",
                table: "samplings");

            migrationBuilder.DropColumn(
                name: "access_description",
                table: "sampling_locations");

            migrationBuilder.DropColumn(
                name: "address",
                table: "sampling_locations");
        }
    }
}
