using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailPreferenceMarkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_preference_markets",
                columns: table => new
                {
                    email_preference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_preference_markets", x => new { x.email_preference_id, x.market_id });
                    table.ForeignKey(
                        name: "fk_email_preference_markets_email_preferences_email_preference",
                        column: x => x.email_preference_id,
                        principalTable: "email_preferences",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_email_preference_markets_markets_market_id",
                        column: x => x.market_id,
                        principalTable: "markets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_preference_markets_market_id",
                table: "email_preference_markets",
                column: "market_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_preference_markets");
        }
    }
}
