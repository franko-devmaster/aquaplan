using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaPlan.Infrastructure.Data.Migrations
{
    /// <summary>
    /// AQ-363 — Fix barcode validation rule.
    ///
    /// Business rule: within a mandate, every container shares the SAME barcode
    /// (it is one physical sample conditioned into several vials).
    /// The canonical barcode is stored on Sampling.SampleBarcode and must be
    /// unique per tenant across mandates.
    ///
    /// Previous model (incorrect): each SamplingContainer carried its own Barcode
    /// with a unique (tenant_id, barcode) index on sampling_containers, which
    /// prevented two vials of the SAME mandate from sharing the same code.
    /// </summary>
    public partial class FixBarcodeValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add tenant_id column on samplings (nullable first for backfill).
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "samplings",
                type: "uuid",
                nullable: true);

            // 2) Backfill: derive the sampling's tenant_id from its order.
            migrationBuilder.Sql(@"
                UPDATE samplings s
                SET tenant_id = o.tenant_id
                FROM orders o
                WHERE o.id = s.order_id
                  AND s.tenant_id IS NULL;
            ");

            // 3) Best-effort backfill of samplings.sample_barcode from the
            //    existing sampling_containers.barcode values (if any), taking the
            //    MIN value when a sampling has several container rows. We log
            //    (via a RAISE NOTICE) any sampling whose containers had
            //    divergent barcodes so operators can follow up.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    diverging_count int;
                BEGIN
                    SELECT COUNT(*) INTO diverging_count
                    FROM (
                        SELECT sampling_id
                        FROM sampling_containers
                        WHERE barcode IS NOT NULL
                        GROUP BY sampling_id
                        HAVING COUNT(DISTINCT barcode) > 1
                    ) t;
                    IF diverging_count > 0 THEN
                        RAISE NOTICE 'FixBarcodeValidation: % sampling(s) had divergent container barcodes. Canonical value will be the MIN(barcode).', diverging_count;
                    END IF;
                END$$;

                UPDATE samplings s
                SET sample_barcode = sub.canonical
                FROM (
                    SELECT sampling_id, MIN(barcode) AS canonical
                    FROM sampling_containers
                    WHERE barcode IS NOT NULL
                    GROUP BY sampling_id
                ) sub
                WHERE sub.sampling_id = s.id
                  AND (s.sample_barcode IS NULL OR s.sample_barcode = '');
            ");

            // 4) Resolve any (tenant_id, sample_barcode) collisions created by the
            //    backfill. The unique index below would fail otherwise. We keep the
            //    oldest sampling (smallest created_at) and clear the barcode on the
            //    duplicates (they become NULL, and operators can rescan if needed).
            migrationBuilder.Sql(@"
                WITH duplicates AS (
                    SELECT id,
                           ROW_NUMBER() OVER (
                               PARTITION BY tenant_id, sample_barcode
                               ORDER BY created_at ASC
                           ) AS rn
                    FROM samplings
                    WHERE sample_barcode IS NOT NULL
                )
                UPDATE samplings
                SET sample_barcode = NULL
                WHERE id IN (SELECT id FROM duplicates WHERE rn > 1);
            ");

            // 5) Enforce NOT NULL on samplings.tenant_id after backfill.
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "samplings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 6) Drop legacy barcode storage on sampling_containers.
            migrationBuilder.DropForeignKey(
                name: "fk_sampling_containers_tenants_tenant_id",
                table: "sampling_containers");

            migrationBuilder.DropIndex(
                name: "ix_samplings_sample_barcode",
                table: "samplings");

            migrationBuilder.DropIndex(
                name: "ix_sampling_containers_tenant_id_barcode",
                table: "sampling_containers");

            migrationBuilder.DropColumn(
                name: "barcode",
                table: "sampling_containers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "sampling_containers");

            // 7) Add the tenant-scoped unique filtered index on samplings.
            migrationBuilder.CreateIndex(
                name: "ix_samplings_tenant_id_sample_barcode",
                table: "samplings",
                columns: new[] { "tenant_id", "sample_barcode" },
                unique: true,
                filter: "sample_barcode IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_samplings_tenants_tenant_id",
                table: "samplings",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_samplings_tenants_tenant_id",
                table: "samplings");

            migrationBuilder.DropIndex(
                name: "ix_samplings_tenant_id_sample_barcode",
                table: "samplings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "samplings");

            migrationBuilder.AddColumn<string>(
                name: "barcode",
                table: "sampling_containers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "sampling_containers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Best-effort restore of sampling_containers.tenant_id from
            // the sampling's order tenant. Barcode values are not restored
            // at container granularity (they were canonicalised on samplings).
            migrationBuilder.Sql(@"
                UPDATE sampling_containers sc
                SET tenant_id = o.tenant_id
                FROM samplings s
                JOIN orders o ON o.id = s.order_id
                WHERE sc.sampling_id = s.id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_samplings_sample_barcode",
                table: "samplings",
                column: "sample_barcode",
                unique: true,
                filter: "sample_barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sampling_containers_tenant_id_barcode",
                table: "sampling_containers",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_sampling_containers_tenants_tenant_id",
                table: "sampling_containers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
