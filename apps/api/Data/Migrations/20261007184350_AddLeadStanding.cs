using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadStanding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "scope",
                table: "permits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_on",
                table: "fire_opportunities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "standing",
                table: "fire_opportunities",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scope",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "last_activity_on",
                table: "fire_opportunities");

            migrationBuilder.DropColumn(
                name: "standing",
                table: "fire_opportunities");
        }
    }
}
