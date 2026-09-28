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

// Humanizes a scraper machine string ("new_installation", "multifamily_residential")
// into a display phrase ("New installation", "Multifamily residential"). Only the
// first word is capitalized. "unknown" (any case), null, and blank input are all
// treated as absent and render as null so callers can hide the field entirely.
export function humanizeMachineString(value: string | null): string | null {
  if (value == null) return null;
  const trimmed = value.trim();
  if (!trimmed) return null;
  if (trimmed.toLowerCase() === "unknown") return null;
  const words = trimmed.toLowerCase().split(/[_\s]+/).filter(Boolean);
  if (words.length === 0) return null;
  return words.map((w, i) => (i === 0 ? w.charAt(0).toUpperCase() + w.slice(1) : w)).join(" ");
}

// True when a field's text would only repeat its card's title, e.g. a system type of
// "Inspection" inside the "Inspection" card. Callers hide such a field.
export function repeatsTitle(value: string | null, title: string): boolean {
  return value != null && value.trim().toLowerCase() === title.trim().toLowerCase();
}

// Permit status display: the mapped status label, plus the raw source text when
// it's present and says something different than the label (e.g. "Failed · Open/Follow-Up Needed").
export function permitStatusDisplay(label: string, rawStatus: string | null): string {
  const raw = rawStatus?.trim();
  if (!raw || raw.toLowerCase() === label.trim().toLowerCase()) return label;
  return `${label} · ${raw}`;
}

export type RecordKind = "permit" | "inspection" | "violation";

// Classifies a permit's recordType into the three shapes the detail page cares
// about. Comparison is case-insensitive and whitespace-tolerant; anything other
// than "inspection" or "violation" (including null/empty) is a regular permit.
export function recordKind(recordType: string | null): RecordKind {
  const normalized = recordType?.trim().toLowerCase();
  if (normalized === "inspection") return "inspection";
  if (normalized === "violation") return "violation";
  return "permit";
}

const webAddress = (value: string | null | undefined): string | null => {
  const trimmed = value?.trim();
  return trimmed && /^https?:\/\//i.test(trimmed) ? trimmed : null;
};

export type RecordLink = { href: string; label: string };

// Picks the link shown on a lead and the words that describe it. Only the city's
// own page for the record is called the original record; a raw data row and the
// dataset's home page are each named for what they are. Null when there is no
// web address to link to.
export function recordLink(source: {
  url: string; recordUrl: string | null; recordUrlKind: string | null;
}): RecordLink | null {
  const record = webAddress(source.recordUrl);
  if (record) {
    return {
      href: record,
      label: source.recordUrlKind === "PAGE" ? "View original record" : "View source data for this record",
    };
  }
  const dataset = webAddress(source.url);
  return dataset ? { href: dataset, label: "View source dataset" } : null;
}
