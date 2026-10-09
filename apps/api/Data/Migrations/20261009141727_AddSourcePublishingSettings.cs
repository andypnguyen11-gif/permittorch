using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePublishingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "latest_record_date",
                table: "sources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "publish_cadence",
                table: "sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "publishes_contractor",
                table: "sources",
                type: "boolean",
                nullable: false,
                // Every source before this one publishes contractors; false would silently move
                // their leads off "no contractor listed".
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "recency_from_first_seen_since",
                table: "sources",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "latest_record_date",
                table: "sources");

            migrationBuilder.DropColumn(
                name: "publish_cadence",
                table: "sources");

            migrationBuilder.DropColumn(
                name: "publishes_contractor",
                table: "sources");

            migrationBuilder.DropColumn(
                name: "recency_from_first_seen_since",
                table: "sources");
        }
    }
}
