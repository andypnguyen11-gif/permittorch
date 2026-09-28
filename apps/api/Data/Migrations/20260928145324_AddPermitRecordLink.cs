using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPermitRecordLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "applicant_name",
                table: "permits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "record_url",
                table: "permits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "record_url_kind",
                table: "permits",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "applicant_name",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "record_url",
                table: "permits");

            migrationBuilder.DropColumn(
                name: "record_url_kind",
                table: "permits");
        }
    }
}
