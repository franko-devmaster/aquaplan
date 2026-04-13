using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "user_number",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.Sql(@"
                WITH numbered AS (
                    SELECT id, ROW_NUMBER() OVER (ORDER BY created_at) + 100000 AS num
                    FROM ""AspNetUsers""
                )
                UPDATE ""AspNetUsers"" SET user_number = numbered.num
                FROM numbered WHERE ""AspNetUsers"".id = numbered.id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_user_number_tenant_id",
                table: "AspNetUsers",
                columns: new[] { "user_number", "tenant_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_user_number_tenant_id",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "user_number",
                table: "AspNetUsers");
        }
    }
}
