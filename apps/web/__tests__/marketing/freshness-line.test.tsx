import { describe, expect, it } from "vitest";
import { relativeUpdatedLabel } from "@/components/marketing/freshness-line";

const NOW = new Date("2026-08-19T12:00:00Z");

describe("relativeUpdatedLabel", () => {
  it("is honest when there is no update time", () => {
    expect(relativeUpdatedLabel(null, NOW)).toBe("Awaiting first data update");
  });
  it("reports minutes, hours, and days", () => {
    expect(relativeUpdatedLabel("2026-08-19T11:55:00Z", NOW)).toBe("Updated 5 minutes ago");
    expect(relativeUpdatedLabel("2026-08-19T06:00:00Z", NOW)).toBe("Updated 6 hours ago");
    expect(relativeUpdatedLabel("2026-08-16T12:00:00Z", NOW)).toBe("Updated 3 days ago");
  });
  it("uses singular units", () => {
    expect(relativeUpdatedLabel("2026-08-19T11:00:00Z", NOW)).toBe("Updated 1 hour ago");
    expect(relativeUpdatedLabel("2026-08-18T12:00:00Z", NOW)).toBe("Updated 1 day ago");
  });
});
