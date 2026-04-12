using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplingBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "barcode_scanned_at",
                table: "samplings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sample_barcode",
                table: "samplings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_samplings_sample_barcode",
                table: "samplings",
                column: "sample_barcode",
                unique: true,
                filter: "sample_barcode IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_samplings_sample_barcode",
                table: "samplings");

            migrationBuilder.DropColumn(
                name: "barcode_scanned_at",
                table: "samplings");

            migrationBuilder.DropColumn(
                name: "sample_barcode",
                table: "samplings");
        }
    }
}
