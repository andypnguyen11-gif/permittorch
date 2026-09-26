import Link from "next/link";
import type { AdminSource, HealthStatus } from "@permittorch/types";
import { SectionCard } from "@/components/app/section-card";
import { formatRelative } from "@/components/app/format";
import { cn } from "@/lib/utils";

export const HEALTH_DOT: Record<HealthStatus, string> = {
  HEALTHY: "bg-green-500", WARNING: "bg-orange-400", STALE: "bg-amber-500",
  FAILED: "bg-red-500", DISABLED: "bg-stone-300",
};
export const HEALTH_LABEL: Record<HealthStatus, string> = {
  HEALTHY: "Good", WARNING: "Warning", STALE: "Stale", FAILED: "Failed", DISABLED: "Disabled",
};
export const HEALTH_TEXT: Record<HealthStatus, string> = {
  HEALTHY: "text-green-700", WARNING: "text-orange-600", STALE: "text-amber-700",
  FAILED: "text-red-600", DISABLED: "text-stone-400",
};

export function SourceHealthPanel({ sources }: { sources: AdminSource[] }) {
  return (
    <SectionCard title="Source health"
      action={<Link href="/app/admin/sources" className="text-sm font-medium text-orange-600 hover:underline">View all</Link>}>
      {sources.length === 0 ? (
        <p className="text-sm text-stone-500">No sources configured yet.</p>
      ) : (
        <ul className="space-y-2.5">
          {sources.slice(0, 5).map((s) => (
            <li key={s.id} className="flex items-center justify-between gap-3 text-sm">
              <span className="flex min-w-0 items-center gap-2">
                <span className={cn("size-2 shrink-0 rounded-full", HEALTH_DOT[s.healthStatus])} aria-hidden />
                <span className="truncate text-stone-700">{s.name}</span>
              </span>
              <span className={cn("shrink-0 text-xs font-medium", HEALTH_TEXT[s.healthStatus])}
                title={`Last successful run ${formatRelative(s.lastSuccessfulRunAt)}`}>
                {HEALTH_LABEL[s.healthStatus]}
              </span>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}
