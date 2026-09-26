import { AWAITING_FIRST_UPDATE } from "@/lib/marketing/freshness";
import { FreshnessLabel } from "@/components/marketing/freshness-label";

export { relativeUpdatedLabel, absoluteUpdatedLabel } from "@/lib/marketing/freshness";

export function FreshnessLine({ lastUpdatedAt }: { lastUpdatedAt: string | null }) {
  return (
    <p className="inline-flex items-center gap-2 text-sm text-neutral-500">
      <span aria-hidden="true"
        className={`h-2 w-2 rounded-full ${lastUpdatedAt ? "bg-emerald-500" : "bg-neutral-300"}`} />
      {lastUpdatedAt ? <FreshnessLabel lastUpdatedAt={lastUpdatedAt} /> : AWAITING_FIRST_UPDATE}
    </p>
  );
}
