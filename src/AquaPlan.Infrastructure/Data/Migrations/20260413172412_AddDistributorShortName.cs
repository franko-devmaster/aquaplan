using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributorShortName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "short_name",
                table: "distributors",
                type: "text",
                nullable: true);

            // Backfill short names for existing distributors
            migrationBuilder.Sql("UPDATE distributors SET short_name = 'AIEB' WHERE name LIKE '%AIEB%'");
            migrationBuilder.Sql("UPDATE distributors SET short_name = 'Bulle' WHERE name LIKE '%Bulle%'");
            migrationBuilder.Sql("UPDATE distributors SET short_name = 'Morat' WHERE name LIKE '%Morat%'");
            migrationBuilder.Sql("UPDATE distributors SET short_name = 'Fribourg' WHERE name LIKE '%Fribourg%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "short_name",
                table: "distributors");
        }
    }
}
