import { CalendarDays, RefreshCw, TriangleAlert } from "lucide-react";
import type { Freshness } from "@permittorch/types";
import { formatDate, formatRelative } from "@/components/app/format";

// Sources are refreshed at least daily; once data is more than a day old we say
// so rather than implying it is current. (No fixed schedule is assumed.)
export const STALE_AFTER_MS = 24 * 60 * 60 * 1000;

// A monthly market is described by the newest permit it holds, on its own line; a daily run
// time never stands for it.
export function FreshnessLine({ freshness }: { freshness: Freshness }) {
  const monthly = freshness.monthlyData ?? [];
  if (monthly.length === 0) return <DailyFreshness lastUpdatedAt={freshness.lastUpdatedAt} />;
  return (
    <div className="space-y-0.5">
      {freshness.lastUpdatedAt !== null && <DailyFreshness lastUpdatedAt={freshness.lastUpdatedAt} />}
      {monthly.map((m) => (
        <p key={m.marketName} className="flex items-center gap-1.5 text-xs text-stone-500">
          <CalendarDays className="size-3" aria-hidden />
          <span>{m.marketName}: data through {formatDate(m.dataThrough)}, published monthly</span>
        </p>
      ))}
    </div>
  );
}

function DailyFreshness({ lastUpdatedAt }: { lastUpdatedAt: string | null }) {
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
