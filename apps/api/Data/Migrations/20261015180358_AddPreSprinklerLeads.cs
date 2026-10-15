using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreSprinklerLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "feed",
                table: "sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "feed",
                table: "scraper_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "pre_sprinkler_leads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    permit_number = table.Column<string>(type: "text", nullable: false),
                    permit_type_label = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    sprinkler_requirement = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: true),
                    filed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    project_name = table.Column<string>(type: "text", nullable: true),
                    street = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    zip = table.Column<string>(type: "text", nullable: true),
                    square_feet = table.Column<int>(type: "integer", nullable: true),
                    size_known = table.Column<bool>(type: "boolean", nullable: false),
                    valuation = table.Column<decimal>(type: "numeric", nullable: true),
                    record_url = table.Column<string>(type: "text", nullable: true),
                    portal_url = table.Column<string>(type: "text", nullable: true),
                    sprinkler_check_status = table.Column<int>(type: "integer", nullable: false),
                    sprinkler_checked_from = table.Column<DateOnly>(type: "date", nullable: true),
                    sprinkler_checked_at = table.Column<DateOnly>(type: "date", nullable: true),
                    sprinkler_matches = table.Column<string>(type: "jsonb", nullable: false),
                    notes = table.Column<string>(type: "jsonb", nullable: false),
                    lead_score = table.Column<int>(type: "integer", nullable: false),
                    standing = table.Column<int>(type: "integer", nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scored_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_run_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pre_sprinkler_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_pre_sprinkler_leads_scraper_runs_last_run_id",
                        column: x => x.last_run_id,
                        principalTable: "scraper_runs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_pre_sprinkler_leads_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pre_sprinkler_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    role_label = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    company = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    license_number = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pre_sprinkler_contacts", x => x.id);
                    table.ForeignKey(
                        name: "fk_pre_sprinkler_contacts_pre_sprinkler_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "pre_sprinkler_leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pre_sprinkler_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_type = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pre_sprinkler_signals", x => x.id);
                    table.ForeignKey(
                        name: "fk_pre_sprinkler_signals_pre_sprinkler_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "pre_sprinkler_leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pre_sprinkler_contacts_lead_id",
                table: "pre_sprinkler_contacts",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_pre_sprinkler_leads_last_run_id",
                table: "pre_sprinkler_leads",
                column: "last_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_pre_sprinkler_leads_source_id_external_id",
                table: "pre_sprinkler_leads",
                columns: new[] { "source_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pre_sprinkler_signals_lead_id",
                table: "pre_sprinkler_signals",
                column: "lead_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pre_sprinkler_contacts");

            migrationBuilder.DropTable(
                name: "pre_sprinkler_signals");

            migrationBuilder.DropTable(
                name: "pre_sprinkler_leads");

            migrationBuilder.DropColumn(
                name: "feed",
                table: "sources");

            migrationBuilder.DropColumn(
                name: "feed",
                table: "scraper_runs");
        }
    }
}
