using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveValidatedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 1 WHERE status = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
