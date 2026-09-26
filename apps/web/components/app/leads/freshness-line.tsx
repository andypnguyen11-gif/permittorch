import { RefreshCw, TriangleAlert } from "lucide-react";
import type { Freshness } from "@permittorch/types";
import { formatRelative } from "@/components/app/format";

// Sources are refreshed at least daily; once data is more than a day old we say
// so rather than implying it is current. (No fixed schedule is assumed.)
export const STALE_AFTER_MS = 24 * 60 * 60 * 1000;

export function FreshnessLine({ freshness }: { freshness: Freshness }) {
  const { lastUpdatedAt } = freshness;
  if (lastUpdatedAt === null) {
    return (
      <p className="flex items-center gap-1.5 text-xs text-stone-500">
        <RefreshCw className="size-3" aria-hidden />
        Freshness unknown
      </p>
    );
  }
  const stale = Date.now() - Date.parse(lastUpdatedAt) > STALE_AFTER_MS;
  return (
    <p className={stale ? "flex items-center gap-1.5 text-xs text-amber-700" : "flex items-center gap-1.5 text-xs text-stone-500"}>
      {stale ? <TriangleAlert className="size-3" aria-hidden /> : <RefreshCw className="size-3" aria-hidden />}
      <time dateTime={lastUpdatedAt} title={new Date(lastUpdatedAt).toUTCString()}>
        Updated {formatRelative(lastUpdatedAt)}
      </time>
      {stale && <span>· Data may be stale</span>}
    </p>
  );
}
