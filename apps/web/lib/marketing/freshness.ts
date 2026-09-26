// Pure freshness-label helpers shared by the server-rendered fallback and the
// client-side relative label. Never compute "N ago" at build time — see
// components/marketing/freshness-label.tsx.

export const AWAITING_FIRST_UPDATE = "Awaiting first data update";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const pad = (n: number) => String(n).padStart(2, "0");

/** Relative label ("Updated 6 hours ago") measured against `now`. */
export function relativeUpdatedLabel(lastUpdatedAt: string | null, now: Date = new Date()): string {
  if (!lastUpdatedAt) return AWAITING_FIRST_UPDATE;
  const ms = now.getTime() - new Date(lastUpdatedAt).getTime();
  const minutes = Math.max(1, Math.floor(ms / 60_000));
  const unit = (n: number, u: string) => `Updated ${n} ${u}${n === 1 ? "" : "s"} ago`;
  if (minutes < 60) return unit(minutes, "minute");
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return unit(hours, "hour");
  return unit(Math.floor(hours / 24), "day");
}

/**
 * Absolute, timezone-explicit label ("Updated Sep 26, 2026 14:05 UTC"). This is
 * what static/no-JS HTML shows: it stays true no matter when the page was built.
 */
export function absoluteUpdatedLabel(lastUpdatedAt: string | null): string {
  if (!lastUpdatedAt) return AWAITING_FIRST_UPDATE;
  const d = new Date(lastUpdatedAt);
  if (Number.isNaN(d.getTime())) return AWAITING_FIRST_UPDATE;
  return `Updated ${MONTHS[d.getUTCMonth()]} ${d.getUTCDate()}, ${d.getUTCFullYear()} `
    + `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())} UTC`;
}
