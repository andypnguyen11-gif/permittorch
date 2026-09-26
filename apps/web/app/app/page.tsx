import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { getAccountMe, getAdminSources, getLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { isUnauthorized } from "@/components/app/session-digest";
import {
  HOT_SCORE, RECENT_DAYS, StatCards, computeOverviewStats,
} from "@/components/app/overview/stat-cards";
import { parseLeadsSearchParams } from "@/components/app/leads/query";
import { SourceHealthPanel } from "@/components/app/overview/source-health-panel";
import { DigestPreview } from "@/components/app/overview/digest-preview";
import { ActivitySparkline } from "@/components/app/overview/activity-sparkline";
import { EmptyState, LeadTable } from "@/components/app/leads/lead-table";
import { FreshnessLine } from "@/components/app/leads/freshness-line";

export const metadata: Metadata = { title: "Overview" };

// Leads the overview samples for the top-leads table, avg score, value and sparkline.
const SAMPLE_SIZE = 100;

export default async function OverviewPage({ searchParams }: {
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
} = {}) {
  const { market } = parseLeadsSearchParams((await searchParams) ?? {});
  const token = await getApiToken();
  // Counts come from API totals (exact for the whole market); only the sample
  // page is used for page-derived numbers, which are labeled as such.
  const [me, leadsRes, hotRes, recentRes] = await Promise.all([
    getAccountMe(token),
    getLeads({ market, pageSize: SAMPLE_SIZE }, token),
    getLeads({ market, minScore: HOT_SCORE, pageSize: 1 }, token),
    getLeads({ market, maxAgeDays: RECENT_DAYS, pageSize: 1 }, token),
  ]).catch((err) => handleApiError(err));
  const isSuperAdmin = me.role === "SUPER_ADMIN";
  // A source-health failure must not take down the overview, but it must not
  // look like "no sources" either: null renders an explicit unavailable state.
  const sources = isSuperAdmin
    ? await getAdminSources(token).catch((err) => (isUnauthorized(err) ? handleApiError(err) : null))
    : null;
  const stats = computeOverviewStats({
    leads: leadsRes.items,
    total: leadsRes.total,
    hotTotal: hotRes.total,
    recentTotal: recentRes.total,
  });
  // The API already orders by score desc, then most recently detected.
  const topLeads = leadsRes.items.slice(0, 5);

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
      <div className="min-w-0 space-y-6">
        <div className="space-y-1">
          <h1 className="text-3xl font-bold tracking-tight">
            Find the permits worth chasing<span className="text-orange-500">.</span>
          </h1>
          <p className="text-sm text-stone-500">
            Scored permit intelligence for fire-protection contractors.
          </p>
          <FreshnessLine freshness={leadsRes.freshness} />
        </div>
        <StatCards stats={stats} />
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Top leads</h2>
            <Link href="/app/leads" className="inline-flex items-center gap-1 text-sm font-medium text-orange-600 hover:underline">
              View all leads <ArrowRight className="size-3.5" aria-hidden />
            </Link>
          </div>
          <LeadTable leads={topLeads} compact
            emptyState={<EmptyState title="No leads in your markets yet"
              description="New fire-protection permits appear here as soon as they’re detected." />} />
        </div>
      </div>
      <div className="space-y-6">
        {isSuperAdmin && <SourceHealthPanel sources={sources} />}
        <DigestPreview me={me} stats={stats} />
        <ActivitySparkline leads={leadsRes.items} total={leadsRes.total} />
      </div>
    </div>
  );
}
