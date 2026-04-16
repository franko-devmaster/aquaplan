using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLatLng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location_lat",
                table: "samplings");

            migrationBuilder.DropColumn(
                name: "location_lng",
                table: "samplings");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "sampling_locations");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "sampling_locations");

            migrationBuilder.DropColumn(
                name: "proposed_latitude",
                table: "sampling_location_change_requests");

            migrationBuilder.DropColumn(
                name: "proposed_longitude",
                table: "sampling_location_change_requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "location_lat",
                table: "samplings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "location_lng",
                table: "samplings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "sampling_locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "sampling_locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "proposed_latitude",
                table: "sampling_location_change_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "proposed_longitude",
                table: "sampling_location_change_requests",
                type: "double precision",
                nullable: true);
        }
    }
}
