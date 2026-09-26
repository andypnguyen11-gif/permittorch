using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "markets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_markets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sample_lead_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    company = table.Column<string>(type: "text", nullable: false),
                    market_slug = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sample_lead_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    market_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    portal_type = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    jurisdiction = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    last_successful_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_record_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    records_last_run = table.Column<int>(type: "integer", nullable: false),
                    health_status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_sources_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    firebase_uid = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_app_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stripe_customer_id = table.Column<string>(type: "text", nullable: false),
                    stripe_subscription_id = table.Column<string>(type: "text", nullable: true),
                    plan = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscriptions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    permit_number = table.Column<string>(type: "text", nullable: true),
                    permit_type = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    raw_status = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    zip = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    filed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issued_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estimated_value = table.Column<decimal>(type: "numeric", nullable: true),
                    square_footage = table.Column<int>(type: "integer", nullable: true),
                    owner_name = table.Column<string>(type: "text", nullable: true),
                    contractor_name = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permits", x => x.id);
                    table.ForeignKey(
                        name: "fk_permits_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scraper_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    apify_run_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    records_imported = table.Column<int>(type: "integer", nullable: false),
                    duplicates_skipped = table.Column<int>(type: "integer", nullable: false),
                    classified = table.Column<int>(type: "integer", nullable: false),
                    failures = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: false),
                    coverage_report_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scraper_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_scraper_runs_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "email_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_preferences_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_markets",
                columns: table => new
                {
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_markets", x => new { x.subscription_id, x.market_id });
                    table.ForeignKey(
                        name: "fk_subscription_markets_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_subscription_markets_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fire_opportunities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    lead_score = table.Column<int>(type: "integer", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    first_detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fire_opportunities", x => x.id);
                    table.ForeignKey(
                        name: "fk_fire_opportunities_permits_permit_id",
                        column: x => x.permit_id,
                        principalTable: "permits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permit_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permit_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_permit_participants_permits_permit_id",
                        column: x => x.permit_id,
                        principalTable: "permits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fire_opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_type = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_signals", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_signals_fire_opportunities_fire_opportunity_id",
                        column: x => x.fire_opportunity_id,
                        principalTable: "fire_opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_leads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fire_opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_leads_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_saved_leads_fire_opportunities_fire_opportunity_id",
                        column: x => x.fire_opportunity_id,
                        principalTable: "fire_opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_users_firebase_uid",
                table: "app_users",
                column: "firebase_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_users_organization_id",
                table: "app_users",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_preferences_user_id",
                table: "email_preferences",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_fire_opportunities_permit_id",
                table: "fire_opportunities",
                column: "permit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lead_signals_fire_opportunity_id",
                table: "lead_signals",
                column: "fire_opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_markets_slug",
                table: "markets",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permit_participants_permit_id",
                table: "permit_participants",
                column: "permit_id");

            migrationBuilder.CreateIndex(
                name: "ix_permits_fingerprint",
                table: "permits",
                column: "fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_permits_source_id_external_id",
                table: "permits",
                columns: new[] { "source_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sample_lead_requests_email_market_slug",
                table: "sample_lead_requests",
                columns: new[] { "email", "market_slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_leads_fire_opportunity_id",
                table: "saved_leads",
                column: "fire_opportunity_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_leads_user_id_fire_opportunity_id",
                table: "saved_leads",
                columns: new[] { "user_id", "fire_opportunity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scraper_runs_source_id",
                table: "scraper_runs",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_sources_market_id",
                table: "sources",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_markets_market_id",
                table: "subscription_markets",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_organization_id",
                table: "subscriptions",
                column: "organization_id",
                unique: true);

            migrationBuilder.Sql(
                "CREATE INDEX ix_permits_fts ON permits USING GIN (to_tsvector('english', coalesce(description,'') || ' ' || coalesce(address,'')));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_permits_fts;");

            migrationBuilder.DropTable(
                name: "email_preferences");

            migrationBuilder.DropTable(
                name: "lead_signals");

            migrationBuilder.DropTable(
                name: "permit_participants");

            migrationBuilder.DropTable(
                name: "sample_lead_requests");

            migrationBuilder.DropTable(
                name: "saved_leads");

            migrationBuilder.DropTable(
                name: "scraper_runs");

            migrationBuilder.DropTable(
                name: "subscription_markets");

            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "fire_opportunities");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "permits");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "sources");

            migrationBuilder.DropTable(
                name: "markets");
        }
    }
}
