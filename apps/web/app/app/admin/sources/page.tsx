import type { Metadata } from "next";
import type { HealthStatus } from "@permittorch/types";
import { getAdminSources } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { handleApiError } from "@/components/app/api-errors";
import { SourceTable } from "@/components/app/admin/source-table";
import { HEALTH_DOT, HEALTH_LABEL } from "@/components/app/overview/source-health-panel";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "Admin · Sources" };

const ORDER: HealthStatus[] = ["HEALTHY", "WARNING", "STALE", "FAILED", "DISABLED"];

export default async function AdminSourcesPage() {
  const token = await requireSuperAdmin();
  const sources = await getAdminSources(token).catch((err) => handleApiError(err));
  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Sources</h1>
        <p className="text-sm text-stone-500">Per-source scraper health. Disable a source to pause its ingestion.</p>
      </div>
      <ul className="flex flex-wrap gap-2" aria-label="Health summary">
        {ORDER.map((status) => (
          <li key={status} className="flex items-center gap-2 rounded-full border border-border bg-white px-3 py-1 text-sm">
            <span className={cn("size-2 rounded-full", HEALTH_DOT[status])} aria-hidden />
            {HEALTH_LABEL[status]}
            <span className="font-semibold tabular-nums">{sources.filter((s) => s.healthStatus === status).length}</span>
          </li>
        ))}
      </ul>
      <SourceTable sources={sources} />
    </div>
  );
}
