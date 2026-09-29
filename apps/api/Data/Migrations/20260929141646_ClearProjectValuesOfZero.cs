using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClearProjectValuesOfZero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A zero is what a portal writes where the filer entered no project value, and the
            // lead showed it as "$0". The scraper stopped sending zeros in build 0.1.16.
            // Ingestion never replaces a stored value with an empty one, so the zeros already
            // stored are cleared here, on every source. No score changes: only a value above
            // 500,000 earns points.
            migrationBuilder.Sql("""
                UPDATE permits SET estimated_value = NULL WHERE estimated_value <= 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: a cleared zero was never a real value.
        }
    }
}
