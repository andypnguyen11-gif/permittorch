using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPermitDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_name",
                table: "permits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "expiration_date",
                table: "permits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "inspection_date",
                table: "permits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property_type",
                table: "permits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "record_type",
                table: "permits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "work_type",
                table: "permits",
                type: "text",
                nullable: true);

            // Participants were never written before this migration. Fill them from the owner
            // and contractor names already on each permit (role 0 = Owner, 2 = Contractor), the
            // same rule ingestion applies from now on. Safe to re-run: it skips a permit that
            // already has a participant in that role.
            migrationBuilder.Sql("""
                INSERT INTO permit_participants (id, permit_id, role, name)
                SELECT gen_random_uuid(), p.id, 0, btrim(p.owner_name)
                FROM permits p
                WHERE btrim(coalesce(p.owner_name, '')) <> ''
                  AND NOT EXISTS (SELECT 1 FROM permit_participants pp
                                  WHERE pp.permit_id = p.id AND pp.role = 0);

                INSERT INTO permit_participants (id, permit_id, role, name)
                SELECT gen_random_uuid(), p.id, 2, btrim(p.contractor_name)
                FROM permits p
                WHERE btrim(coalesce(p.contractor_name, '')) <> ''
                  AND NOT EXISTS (SELECT 1 FROM permit_participants pp
                                  WHERE pp.permit_id = p.id AND pp.role = 2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Before this migration nothing wrote Owner (0) or Contractor (2) participants, so
            // removing them restores the earlier state exactly.
            migrationBuilder.Sql("DELETE FROM permit_participants WHERE role IN (0, 2);");

            migrationBuilder.DropColumn(
                name: "business_name",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "expiration_date",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "inspection_date",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "property_type",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "record_type",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "work_type",
                table: "permits");
        }
    }
}
