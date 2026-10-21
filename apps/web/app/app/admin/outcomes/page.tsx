import type { Metadata } from "next";
import { getLeadOutcomeReport, getMarkets } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { handleApiError } from "@/components/app/api-errors";
import { OutcomeReportTable } from "@/components/app/admin/outcome-report-table";
import { Button } from "@/components/ui/button";

export const metadata: Metadata = { title: "Admin · Lead tags" };

const field = "h-9 rounded-lg border border-border bg-white px-2 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50";

export default async function AdminOutcomesPage({ searchParams }: {
  searchParams: Promise<{ marketId?: string | string[] }>;
}) {
  const token = await requireSuperAdmin();
  const markets = await getMarkets().catch((err) => handleApiError(err));
  const requested = (await searchParams).marketId;
  // Only a market the list knows is sent, so a hand-edited URL cannot reach the API as junk.
  const marketId = markets.find((m) => m.id === requested)?.id;
  const report = await getLeadOutcomeReport(token, marketId).catch((err) => handleApiError(err));

  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Lead tags</h1>
        <p className="max-w-2xl text-sm text-stone-500">
          How contractors tagged leads, by the score each lead had when it was tagged. Staff tags are not counted.
        </p>
      </div>
      <form method="get" aria-label="Filter lead tags" className="flex flex-wrap items-end gap-3 rounded-xl border border-border bg-white p-3">
        <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Market
          <select name="marketId" defaultValue={marketId ?? ""} className={field}>
            <option value="">All markets</option>
            {markets.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </select>
        </label>
        <Button type="submit" variant="outline" className="h-9">Apply</Button>
      </form>
      <OutcomeReportTable report={report} />
    </div>
  );
}
