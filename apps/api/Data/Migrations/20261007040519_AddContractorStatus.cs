using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractorStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "contractor_status",
                table: "fire_opportunities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_fire_opportunities_contractor_status",
                table: "fire_opportunities",
                column: "contractor_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fire_opportunities_contractor_status",
                table: "fire_opportunities");

            migrationBuilder.DropColumn(
                name: "contractor_status",
                table: "fire_opportunities");
        }
    }
}
