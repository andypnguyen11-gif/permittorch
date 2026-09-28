using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "permit_participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "license_number",
                table: "permit_participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "permit_participants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "permit_participants");

            migrationBuilder.DropColumn(
                name: "license_number",
                table: "permit_participants");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "permit_participants");
        }
    }
}
