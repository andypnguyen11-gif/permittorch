// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { renderToStaticMarkup } from "react-dom/server";
import {
  FreshnessLine, absoluteUpdatedLabel, relativeUpdatedLabel,
} from "@/components/marketing/freshness-line";

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

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

describe("absoluteUpdatedLabel", () => {
  it("formats an explicit UTC timestamp", () => {
    expect(absoluteUpdatedLabel("2026-09-26T14:05:00Z")).toBe("Updated Sep 26, 2026 14:05 UTC");
  });
  it("is honest when there is no update time", () => {
    expect(absoluteUpdatedLabel(null)).toBe("Awaiting first data update");
  });
});

describe("FreshnessLine", () => {
  const ISO = "2026-08-19T06:00:00Z";

  it("server-renders an absolute timestamp, never a build-time relative label", () => {
    const html = renderToStaticMarkup(<FreshnessLine lastUpdatedAt={ISO} />);
    expect(html.toLowerCase()).toContain(`<time datetime="${ISO}"`.toLowerCase());
    expect(html).toContain("Updated Aug 19, 2026 06:00 UTC");
    expect(html).not.toMatch(/ago/);
  });

  it("computes the relative label from the viewer's current clock", () => {
    vi.useFakeTimers({ toFake: ["Date", "setInterval", "clearInterval"] });
    vi.setSystemTime(new Date("2026-08-19T12:00:00Z"));
    render(<FreshnessLine lastUpdatedAt={ISO} />);
    const time = screen.getByText("Updated 6 hours ago");
    expect(time.tagName).toBe("TIME");
    expect(time).toHaveAttribute("datetime", ISO);
  });

  it("uses a different clock to produce a different label (not a constant)", () => {
    vi.useFakeTimers({ toFake: ["Date", "setInterval", "clearInterval"] });
    vi.setSystemTime(new Date("2026-08-22T06:00:00Z"));
    render(<FreshnessLine lastUpdatedAt={ISO} />);
    expect(screen.getByText("Updated 3 days ago")).toBeDefined();
  });

  it("recomputes the label every minute while mounted", () => {
    vi.useFakeTimers({ toFake: ["Date", "setInterval", "clearInterval"] });
    vi.setSystemTime(new Date("2026-08-19T06:58:30Z"));
    render(<FreshnessLine lastUpdatedAt={ISO} />);
    expect(screen.getByText("Updated 58 minutes ago")).toBeDefined();
    act(() => { vi.advanceTimersByTime(120_000); });
    expect(screen.getByText("Updated 1 hour ago")).toBeDefined();
  });

  it("renders the honest waiting text when there is no data yet", () => {
    render(<FreshnessLine lastUpdatedAt={null} />);
    expect(screen.getByText("Awaiting first data update")).toBeDefined();
    expect(document.querySelector("time")).toBeNull();
  });
});
