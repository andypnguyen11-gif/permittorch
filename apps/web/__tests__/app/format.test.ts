import { describe, expect, it } from "vitest";
import { formatDate, formatRelative, formatValueShort, scoreBand } from "@/components/app/format";

describe("scoreBand", () => {
  it("bands scores per mockup: >=90 hot, 80-89 strong, 70-79 medium, else muted", () => {
    expect(scoreBand(94)).toBe("hot");
    expect(scoreBand(90)).toBe("hot");
    expect(scoreBand(89)).toBe("strong");
    expect(scoreBand(80)).toBe("strong");
    expect(scoreBand(79)).toBe("medium");
    expect(scoreBand(70)).toBe("medium");
    expect(scoreBand(69)).toBe("muted");
    expect(scoreBand(41)).toBe("muted");
  });
});

describe("formatRelative", () => {
  const now = Date.parse("2026-08-19T12:00:00Z");
  it("renders minutes, hours, days, and null", () => {
    expect(formatRelative("2026-08-19T11:59:40Z", now)).toBe("just now");
    expect(formatRelative("2026-08-19T11:42:00Z", now)).toBe("18 minutes ago");
    expect(formatRelative("2026-08-19T09:00:00Z", now)).toBe("3 hours ago");
    expect(formatRelative("2026-08-17T12:00:00Z", now)).toBe("2 days ago");
    expect(formatRelative("2026-08-19T11:59:00Z", now)).toBe("1 minute ago");
    expect(formatRelative(null, now)).toBe("—");
  });
});

describe("formatValueShort", () => {
  it("abbreviates currency like the mockup", () => {
    expect(formatValueShort(1850000)).toBe("$1.85M");
    expect(formatValueShort(24600000)).toBe("$24.6M");
    expect(formatValueShort(620000)).toBe("$620K");
    expect(formatValueShort(150000)).toBe("$150K");
    expect(formatValueShort(15000)).toBe("$15K");
    expect(formatValueShort(950)).toBe("$950");
    expect(formatValueShort(null)).toBe("—");
  });
});

describe("formatDate", () => {
  it("renders a medium date or em dash", () => {
    expect(formatDate("2025-05-13T10:00:00Z")).toBe("May 13, 2025");
    expect(formatDate(null)).toBe("—");
  });
});
