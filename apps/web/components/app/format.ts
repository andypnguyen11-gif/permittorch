export type ScoreBand = "hot" | "strong" | "medium" | "muted";

export function scoreBand(score: number): ScoreBand {
  if (score >= 90) return "hot";
  if (score >= 80) return "strong";
  if (score >= 70) return "medium";
  return "muted";
}

export function formatRelative(iso: string | null, now: number = Date.now()): string {
  if (iso == null) return "—";
  const diffMs = Math.max(0, now - Date.parse(iso));
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? "" : "s"} ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hour${hours === 1 ? "" : "s"} ago`;
  const days = Math.floor(hours / 24);
  return `${days} day${days === 1 ? "" : "s"} ago`;
}

// Strips zeros only after a decimal point ("24.60"→"24.6", "2.00"→"2"),
// never from whole numbers ("150" stays "150").
const trimZeros = (n: number, digits: number): string =>
  n.toFixed(digits).replace(/\.0+$|(\.\d*?)0+$/, "$1");

export function formatValueShort(value: number | null): string {
  if (value == null) return "—";
  if (value >= 1_000_000) return `$${trimZeros(value / 1_000_000, 2)}M`;
  if (value >= 1_000) return `$${Math.round(value / 1_000)}K`;
  return `$${Math.round(value)}`;
}

export function formatDate(iso: string | null): string {
  if (iso == null) return "—";
  return new Date(iso).toLocaleDateString("en-US", {
    month: "short", day: "numeric", year: "numeric", timeZone: "UTC",
  });
}
