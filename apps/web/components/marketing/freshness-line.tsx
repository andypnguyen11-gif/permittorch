export function relativeUpdatedLabel(lastUpdatedAt: string | null, now: Date = new Date()): string {
  if (!lastUpdatedAt) return "Awaiting first data update";
  const ms = now.getTime() - new Date(lastUpdatedAt).getTime();
  const minutes = Math.max(1, Math.floor(ms / 60_000));
  const unit = (n: number, u: string) => `Updated ${n} ${u}${n === 1 ? "" : "s"} ago`;
  if (minutes < 60) return unit(minutes, "minute");
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return unit(hours, "hour");
  return unit(Math.floor(hours / 24), "day");
}

export function FreshnessLine({ lastUpdatedAt }: { lastUpdatedAt: string | null }) {
  return (
    <p className="inline-flex items-center gap-2 text-sm text-neutral-500">
      <span aria-hidden="true"
        className={`h-2 w-2 rounded-full ${lastUpdatedAt ? "bg-emerald-500" : "bg-neutral-300"}`} />
      {relativeUpdatedLabel(lastUpdatedAt)}
    </p>
  );
}
