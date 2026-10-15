import type { Metadata } from "next";
import { getPreSprinklerLeads, getPreSprinklerMarkets } from "@/lib/api";
import {
  PRE_SPRINKLER_KINDS as KINDS, PRE_SPRINKLER_STANDINGS as STANDINGS, readPreSprinklerQuery, type SearchParams,
} from "@/components/app/admin/pre-sprinkler-query";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { handleApiError } from "@/components/app/api-errors";
import { PreSprinklerTable } from "@/components/app/admin/pre-sprinkler-table";
import { PreSprinklerExportButton } from "@/components/app/admin/pre-sprinkler-export-button";
import { Button } from "@/components/ui/button";

export const metadata: Metadata = { title: "Admin · Pre-sprinkler leads" };

const field = "h-9 rounded-lg border border-border bg-white px-2 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/50";

export default async function AdminPreSprinklerPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const token = await requireSuperAdmin();
  const sp = await searchParams;
  const markets = await getPreSprinklerMarkets(token).catch((err) => handleApiError(err));
  const query = readPreSprinklerQuery(sp, markets.map((m) => m.slug));
  const leads = await getPreSprinklerLeads({ ...query, pageSize: 200 }, token).catch((err) => handleApiError(err));

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-2xl font-bold tracking-tight">Pre-sprinkler leads</h1>
          <p className="max-w-2xl text-sm text-stone-500">
            Building permits for new homes, ADUs and additions with no fire sprinkler application found at the address yet. Admin only: subscribers never see these.
          </p>
        </div>
        <PreSprinklerExportButton query={query} />
      </div>
      <form method="get" aria-label="Filter pre-sprinkler leads" className="flex flex-wrap items-end gap-3 rounded-xl border border-border bg-white p-3">
        <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Market
          <select name="market" defaultValue={query.market ?? ""} className={field}>
            <option value="">All four markets</option>
            {markets.map((m) => <option key={m.slug} value={m.slug}>{m.name}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Kind
          <select name="kind" defaultValue={query.kind ?? ""} className={field}>
            <option value="">Every kind</option>
            {KINDS.map((k) => <option key={k.value} value={k.value}>{k.label}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Standing
          <select name="standing" defaultValue={query.standing} className={field}>
            {STANDINGS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Filed from
          <input type="date" name="filedFrom" defaultValue={query.filedFrom ?? ""} className={field} />
        </label>
        <label className="flex flex-col gap-1 text-xs font-medium text-stone-600">Filed to
          <input type="date" name="filedTo" defaultValue={query.filedTo ?? ""} className={field} />
        </label>
        <Button type="submit" className="h-9 px-4">Apply</Button>
      </form>
      <PreSprinklerTable leads={leads.items} />
      <p className="text-sm text-stone-500">
        {leads.total} {leads.total === 1 ? "lead" : "leads"}
        {leads.total > leads.items.length && ` (showing the top ${leads.items.length}; the CSV holds them all)`}
      </p>
    </div>
  );
}
