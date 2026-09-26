import type { Metadata } from "next";
import { getLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { parseLeadsSearchParams } from "@/components/app/leads/query";
import { FilterBar } from "@/components/app/leads/filter-bar";
import { LeadTable } from "@/components/app/leads/lead-table";
import { LeadsPagination } from "@/components/app/leads/pagination";
import { FreshnessLine } from "@/components/app/leads/freshness-line";

export const metadata: Metadata = { title: "Leads" };

export default async function LeadsPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const query = parseLeadsSearchParams(await searchParams);
  const token = await getApiToken();
  const res = await getLeads(query, token).catch((err) => handleApiError(err));

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-2xl font-bold tracking-tight">Leads</h1>
          <FreshnessLine freshness={res.freshness} />
        </div>
        <p className="text-sm text-stone-500">
          <span className="font-semibold text-stone-900 tabular-nums">{res.total}</span>{" "}
          {res.total === 1 ? "opportunity" : "opportunities"}
        </p>
      </div>
      <FilterBar query={query} />
      {query.q && (
        <p className="text-sm text-stone-500">
          Search results for <span className="font-medium text-stone-900">“{query.q}”</span>
        </p>
      )}
      <LeadTable leads={res.items} className="max-h-[calc(100dvh-19rem)] min-h-64" />
      <LeadsPagination query={query} total={res.total} />
    </div>
  );
}
