using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermitTorch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MovePermitNamesToTheirRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Until scraper build 0.1.16 two sources sent a name in the contractor field that
            // was not the contractor: Chicago sent the owner and Nashville the applicant. The
            // names are kept and moved to the role they belong to (participant role 0 = Owner,
            // 1 = Applicant, 2 = Contractor). An owner or applicant already stored wins. The
            // daily rescoring pass then scores these permits as having no contractor listed.
            migrationBuilder.Sql("""
                DELETE FROM permit_participants pp
                USING permits p, sources s
                WHERE pp.permit_id = p.id AND p.source_id = s.id AND pp.role = 2
                  AND s.jurisdiction IN ('chicago-building-permits', 'nashville-building-permits');

                UPDATE permits p
                SET owner_name = CASE WHEN btrim(coalesce(p.owner_name, '')) = ''
                                      THEN btrim(p.contractor_name) ELSE p.owner_name END,
                    contractor_name = NULL
                FROM sources s
                WHERE p.source_id = s.id AND s.jurisdiction = 'chicago-building-permits'
                  AND btrim(coalesce(p.contractor_name, '')) <> '';

                UPDATE permits p
                SET applicant_name = CASE WHEN btrim(coalesce(p.applicant_name, '')) = ''
                                          THEN btrim(p.contractor_name) ELSE p.applicant_name END,
                    contractor_name = NULL
                FROM sources s
                WHERE p.source_id = s.id AND s.jurisdiction = 'nashville-building-permits'
                  AND btrim(coalesce(p.contractor_name, '')) <> '';

                INSERT INTO permit_participants (id, permit_id, role, name)
                SELECT gen_random_uuid(), p.id, 0, btrim(p.owner_name)
                FROM permits p JOIN sources s ON s.id = p.source_id
                WHERE s.jurisdiction = 'chicago-building-permits'
                  AND btrim(coalesce(p.owner_name, '')) <> ''
                  AND NOT EXISTS (SELECT 1 FROM permit_participants pp
                                  WHERE pp.permit_id = p.id AND pp.role = 0);

                INSERT INTO permit_participants (id, permit_id, role, name)
                SELECT gen_random_uuid(), p.id, 1, btrim(p.applicant_name)
                FROM permits p JOIN sources s ON s.id = p.source_id
                WHERE s.jurisdiction = 'nashville-building-permits'
                  AND btrim(coalesce(p.applicant_name, '')) <> ''
                  AND NOT EXISTS (SELECT 1 FROM permit_participants pp
                                  WHERE pp.permit_id = p.id AND pp.role = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forward-only: once moved, a name that was already an owner or applicant cannot be
            // told apart from one that came from the contractor field.
        }
    }
}
