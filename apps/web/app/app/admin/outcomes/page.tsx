import type { Metadata } from "next";
import { getLeadOutcomeReport, getMarkets } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { handleApiError } from "@/components/app/api-errors";
import { OutcomeReportTable } from "@/components/app/admin/outcome-report-table";
import { OutcomeReportFilters, reportFilters } from "@/components/app/admin/outcome-report-filters";

export const metadata: Metadata = { title: "Admin · Lead tags" };

export default async function AdminOutcomesPage({ searchParams }: {
  searchParams: Promise<{ marketId?: string | string[]; version?: string | string[]; permitAge?: string | string[] }>;
}) {
  const token = await requireSuperAdmin();
  const markets = await getMarkets().catch((err) => handleApiError(err));
  const filters = reportFilters(await searchParams, markets);
  const report = await getLeadOutcomeReport(token, filters).catch((err) => handleApiError(err));

  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Lead tags</h1>
        <p className="max-w-2xl text-sm text-stone-500">
          How contractors tagged leads, by the score each lead had when it was tagged. Each scoring version is counted on its own, so a reweighting never mixes old and new scores. Staff tags are not counted.
        </p>
      </div>
      <OutcomeReportFilters markets={markets} marketId={filters.marketId} report={report} />
      <OutcomeReportTable report={report} />
    </div>
  );
}
