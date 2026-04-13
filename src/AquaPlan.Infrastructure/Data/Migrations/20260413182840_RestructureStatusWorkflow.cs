using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RestructureStatusWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── OrderStatus conversion ──
            // Old: Draft=0, Assigned=1, InProgress=2, SamplingCompleted=3, Validated=4, SentToLims=5, ResultsReceived=6, Completed=7, Cancelled=8
            // New: New=0, InProgress=1, Completed=2, Transmitted=3, Done=4, Cancelled=5
            //
            // Must convert in reverse order (highest first) to avoid collisions.
            // Use a temporary value (-1) for statuses that would collide.

            // Step 1: Convert statuses that would collide by first moving them to temp values
            // Cancelled: 8 → 5 (would collide with old SentToLims=5, but SentToLims moves to 3)
            // We process from highest old value to lowest to avoid overwrites.

            // Cancelled: 8 → -1 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -1 WHERE status = 8;");
            // Completed: 7 → -2 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -2 WHERE status = 7;");
            // ResultsReceived: 6 → -3 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -3 WHERE status = 6;");
            // SentToLims: 5 → -4 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -4 WHERE status = 5;");
            // Validated: 4 → -5 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -5 WHERE status = 4;");
            // SamplingCompleted: 3 → -6 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -6 WHERE status = 3;");
            // InProgress: 2 → -7 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -7 WHERE status = 2;");
            // Assigned: 1 → -8 (temp)
            migrationBuilder.Sql("UPDATE orders SET status = -8 WHERE status = 1;");
            // Draft: 0 stays as 0 (New=0), no change needed

            // Step 2: Convert temp values to new values
            migrationBuilder.Sql("UPDATE orders SET status = 0 WHERE status = -8;");   // Assigned → New
            migrationBuilder.Sql("UPDATE orders SET status = 1 WHERE status = -7;");   // InProgress → InProgress
            migrationBuilder.Sql("UPDATE orders SET status = 2 WHERE status = -6;");   // SamplingCompleted → Completed
            migrationBuilder.Sql("UPDATE orders SET status = 2 WHERE status = -5;");   // Validated → Completed
            migrationBuilder.Sql("UPDATE orders SET status = 3 WHERE status = -4;");   // SentToLims → Transmitted
            migrationBuilder.Sql("UPDATE orders SET status = 4 WHERE status = -3;");   // ResultsReceived → Done
            migrationBuilder.Sql("UPDATE orders SET status = 4 WHERE status = -2;");   // Completed → Done
            migrationBuilder.Sql("UPDATE orders SET status = 5 WHERE status = -1;");   // Cancelled → Cancelled

            // ── SamplingRoundStatus conversion ──
            // Old: Draft=0, Assigned=1, InProgress=2, Completed=3, Cancelled=4
            // New: Draft=0, Assigned=1, Validated=2, InProgress=3, Completed=4, Cancelled=5
            //
            // Draft=0 → 0 (no change)
            // Assigned=1 → 1 (no change)
            // InProgress=2 → 3, Completed=3 → 4, Cancelled=4 → 5

            // Process from highest to lowest to avoid collisions
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = -1 WHERE status = 4;"); // Cancelled → temp
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = -2 WHERE status = 3;"); // Completed → temp
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = -3 WHERE status = 2;"); // InProgress → temp

            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 3 WHERE status = -3;"); // InProgress → 3
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 4 WHERE status = -2;"); // Completed → 4
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 5 WHERE status = -1;"); // Cancelled → 5
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Reverse SamplingRoundStatus ──
            // New: Draft=0, Assigned=1, Validated=2, InProgress=3, Completed=4, Cancelled=5
            // Old: Draft=0, Assigned=1, InProgress=2, Completed=3, Cancelled=4
            // Note: Validated=2 has no old equivalent; convert to Assigned=1

            migrationBuilder.Sql("UPDATE sampling_rounds SET status = -1 WHERE status = 5;"); // Cancelled → temp
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = -2 WHERE status = 4;"); // Completed → temp
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = -3 WHERE status = 3;"); // InProgress → temp
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 1 WHERE status = 2;");  // Validated → Assigned (lossy)

            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 2 WHERE status = -3;"); // InProgress → 2
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 3 WHERE status = -2;"); // Completed → 3
            migrationBuilder.Sql("UPDATE sampling_rounds SET status = 4 WHERE status = -1;"); // Cancelled → 4

            // ── Reverse OrderStatus ──
            // New: New=0, InProgress=1, Completed=2, Transmitted=3, Done=4, Cancelled=5
            // Old: Draft=0, Assigned=1, InProgress=2, SamplingCompleted=3, Validated=4, SentToLims=5, ResultsReceived=6, Completed=7, Cancelled=8
            // Note: New→Draft (lossy, Assigned info lost), Completed→SamplingCompleted (lossy, Validated info lost), Done→Completed (lossy, ResultsReceived info lost)

            migrationBuilder.Sql("UPDATE orders SET status = -1 WHERE status = 5;"); // Cancelled → temp
            migrationBuilder.Sql("UPDATE orders SET status = -2 WHERE status = 4;"); // Done → temp
            migrationBuilder.Sql("UPDATE orders SET status = -3 WHERE status = 3;"); // Transmitted → temp
            migrationBuilder.Sql("UPDATE orders SET status = -4 WHERE status = 2;"); // Completed → temp
            migrationBuilder.Sql("UPDATE orders SET status = -5 WHERE status = 1;"); // InProgress → temp
            // New=0 → Draft=0, no change

            migrationBuilder.Sql("UPDATE orders SET status = 2 WHERE status = -5;"); // InProgress → 2
            migrationBuilder.Sql("UPDATE orders SET status = 3 WHERE status = -4;"); // Completed → SamplingCompleted(3)
            migrationBuilder.Sql("UPDATE orders SET status = 5 WHERE status = -3;"); // Transmitted → SentToLims(5)
            migrationBuilder.Sql("UPDATE orders SET status = 7 WHERE status = -2;"); // Done → Completed(7)
            migrationBuilder.Sql("UPDATE orders SET status = 8 WHERE status = -1;"); // Cancelled → 8
        }
    }
}
