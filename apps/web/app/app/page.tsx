import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { getAccountMe, getAdminSources, getLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { handleApiError } from "@/components/app/api-errors";
import { StatCards, computeOverviewStats } from "@/components/app/overview/stat-cards";
import { SourceHealthPanel } from "@/components/app/overview/source-health-panel";
import { DigestPreview } from "@/components/app/overview/digest-preview";
import { ActivitySparkline } from "@/components/app/overview/activity-sparkline";
import { EmptyState, LeadTable } from "@/components/app/leads/lead-table";
import { FreshnessLine } from "@/components/app/leads/freshness-line";

export const metadata: Metadata = { title: "Overview" };

export default async function OverviewPage() {
  const token = await getApiToken();
  const [me, leadsRes] = await Promise.all([
    getAccountMe(token),
    getLeads({ pageSize: 100 }, token),
  ]).catch((err) => handleApiError(err));
  const isSuperAdmin = me.role === "SUPER_ADMIN";
  const sources = isSuperAdmin ? await getAdminSources(token).catch(() => []) : [];
  const stats = computeOverviewStats(leadsRes.items);
  const topLeads = [...leadsRes.items].sort((a, b) => b.score - a.score).slice(0, 5);

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
        <StatCards leads={leadsRes.items} />
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
        <ActivitySparkline leads={leadsRes.items} />
      </div>
    </div>
  );
}
