using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSamplingRoundConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Polish F-216 — `xmin` is a PostgreSQL *system* column present on every table and is
            // mapped in the model as the SamplingRound optimistic-concurrency token. The scaffolder
            // emits an AddColumn for it, but running `ALTER TABLE sampling_rounds ADD COLUMN xmin`
            // would fail (the system column already exists). The DDL is intentionally removed: this
            // migration only records the model change in the snapshot, no schema change is applied.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: xmin is a system column and was never created by Up().
        }
    }
}
