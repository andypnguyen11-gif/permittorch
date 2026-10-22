using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scoring_version",
                table: "lead_outcomes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scoring_version",
                table: "fire_opportunities",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scoring_version",
                table: "lead_outcomes");

            migrationBuilder.DropColumn(
                name: "scoring_version",
                table: "fire_opportunities");
        }
    }
}
