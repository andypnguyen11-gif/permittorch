import type { Metadata } from "next";
import Link from "next/link";
import { getAdminRuns, getAdminSources } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { handleApiError } from "@/components/app/api-errors";
import { RunsTable } from "@/components/app/admin/runs-table";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "Admin · Runs" };

export default async function AdminRunsPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const token = await requireSuperAdmin();
  const sp = await searchParams;
  const sources = await getAdminSources(token).catch((err) => handleApiError(err));
  const requested = typeof sp.sourceId === "string" ? sp.sourceId : undefined;
  // Only filter by a source that actually exists; ignore junk query values.
  const sourceId = sources.some((s) => s.id === requested) ? requested : undefined;
  const runs = await getAdminRuns({ sourceId }, token).catch((err) => handleApiError(err));

  const chipClass = (active: boolean) =>
    cn(
      "rounded-full border px-3 py-1 text-sm outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50",
      active
        ? "border-orange-300 bg-orange-50 font-medium text-orange-700"
        : "border-border bg-white text-stone-600 hover:border-stone-300",
    );

  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Scraper runs</h1>
        <p className="text-sm text-stone-500">Recent Apify runs with import counts; failures highlighted.</p>
      </div>
      <nav aria-label="Filter runs by source" className="flex flex-wrap gap-2">
        <Link href="/app/admin/runs" aria-current={sourceId === undefined ? "page" : undefined}
          className={chipClass(sourceId === undefined)}>All sources</Link>
        {sources.map((s) => (
          <Link key={s.id} href={`/app/admin/runs?sourceId=${encodeURIComponent(s.id)}`}
            aria-current={sourceId === s.id ? "page" : undefined}
            className={chipClass(sourceId === s.id)}>
            {s.name}
          </Link>
        ))}
      </nav>
      <RunsTable runs={runs.items} />
      <p className="text-sm text-stone-500">{runs.total} {runs.total === 1 ? "run" : "runs"}</p>
    </div>
  );
}
