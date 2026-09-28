using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlaceholderOwnersAndTestPermit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two kinds of stored data that were never real, both stopped at the source by
            // scraper build 0.1.17. Ingestion never replaces a stored value with an empty one,
            // so what is already stored has to be removed here.
            //
            // 1. New York's owner-business column holds placeholders where the filer had
            //    nothing to enter. They reached business_name, and owner_name where the record
            //    named no person. Whole-value matches only, without regard to case or spacing:
            //    "PR REALTY LLC" is a real owner and is kept.
            // 2. Omaha's ALRM-26-00315 is the city's own test record. Its lead, signals,
            //    participants and saved-lead rows go with it (cascading foreign keys).
            migrationBuilder.Sql("""
                CREATE TEMPORARY TABLE placeholder_owner (value text PRIMARY KEY) ON COMMIT DROP;
                INSERT INTO placeholder_owner (value) VALUES
                    ('pr'), ('not applicable'), ('n/a'), ('na'), ('owners rep'), ('private'),
                    ('none'), ('homeowner'), ('authorized signatory for entity');

                DELETE FROM permit_participants pp
                USING permits p, sources s
                WHERE pp.permit_id = p.id AND p.source_id = s.id AND pp.role = 0
                  AND s.jurisdiction = 'nyc-dobnow-permits'
                  AND lower(regexp_replace(btrim(pp.name), '\s+', ' ', 'g'))
                      IN (SELECT value FROM placeholder_owner);

                UPDATE permits p
                SET owner_name = NULL
                FROM sources s
                WHERE p.source_id = s.id AND s.jurisdiction = 'nyc-dobnow-permits'
                  AND lower(regexp_replace(btrim(p.owner_name), '\s+', ' ', 'g'))
                      IN (SELECT value FROM placeholder_owner);

                UPDATE permits p
                SET business_name = NULL
                FROM sources s
                WHERE p.source_id = s.id AND s.jurisdiction = 'nyc-dobnow-permits'
                  AND lower(regexp_replace(btrim(p.business_name), '\s+', ' ', 'g'))
                      IN (SELECT value FROM placeholder_owner);

                DROP TABLE placeholder_owner;

                DELETE FROM permits p
                USING sources s
                WHERE p.source_id = s.id AND s.jurisdiction = 'omaha-fire-permits'
                  AND p.external_id = 'omaha-fire-permits:ALRM-26-00315';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: the removed values were never real data.
        }
    }
}
