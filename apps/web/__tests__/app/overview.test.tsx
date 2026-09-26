// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import type { LeadSummary } from "@permittorch/types";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));

import OverviewPage from "@/app/app/page";
import * as api from "@/lib/api";
import { computeOverviewStats, sampleScope } from "@/components/app/overview/stat-cards";
import { dailyCounts } from "@/components/app/overview/activity-sparkline";
import { digestScheduleLabel } from "@/components/app/overview/digest-preview";
import { mockAccountMe } from "@/lib/fixtures/account";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

const DAY = 86_400_000;
const lead = (o: Partial<LeadSummary>): LeadSummary => ({
  id: "x", score: 50, title: "t", address: null, city: "Houston", state: "TX",
  category: "FIRE_ALARM", permitType: null, status: "NEW", filedDate: null,
  estimatedValue: null, reason: "r", isNew: false, ...o,
});

describe("computeOverviewStats", () => {
  it("takes counts from API totals and page-derived numbers from the sample", () => {
    const leads = [lead({ score: 80, estimatedValue: 100 }), lead({ score: 91, estimatedValue: null })];
    expect(computeOverviewStats({ leads, total: 50, hotTotal: 12, recentTotal: 7 })).toEqual({
      total: 50, hotLeads: 12, filedRecently: 7, avgScore: 86, totalValue: 100, sampleSize: 2,
    });
  });
  it("handles no leads without dividing by zero", () => {
    expect(computeOverviewStats({ leads: [], total: 0, hotTotal: 0, recentTotal: 0 })).toEqual({
      total: 0, hotLeads: 0, filedRecently: 0, avgScore: 0, totalValue: 0, sampleSize: 0,
    });
  });
});

describe("sampleScope", () => {
  it("says 'top n' whenever the sample is partial", () => {
    expect(sampleScope({ total: 250, sampleSize: 100 })).toBe("across your top 100 leads");
    expect(sampleScope({ total: 25, sampleSize: 25 })).toBe("across all 25 leads");
    expect(sampleScope({ total: 1, sampleSize: 1 })).toBe("across your 1 lead");
  });
});

describe("dailyCounts", () => {
  it("buckets filings by age, oldest first, ignoring null and out-of-window dates", () => {
    const now = Date.parse("2026-09-26T12:00:00Z");
    const iso = (daysAgo: number) => new Date(now - daysAgo * DAY).toISOString();
    const counts = dailyCounts([
      lead({ filedDate: iso(0.2) }), lead({ filedDate: iso(0.5) }), lead({ filedDate: iso(2.5) }),
      lead({ filedDate: iso(40) }), lead({ filedDate: null }),
    ], 30, now);
    expect(counts).toHaveLength(30);
    expect(counts[29]).toBe(2);
    expect(counts[27]).toBe(1);
    expect(counts.reduce((a, b) => a + b, 0)).toBe(3);
  });
});

describe("digestScheduleLabel", () => {
  it("describes each frequency honestly", () => {
    expect(digestScheduleLabel("DAILY")).toBe("Next digest: tomorrow morning");
    expect(digestScheduleLabel("WEEKLY")).toBe("Next digest: Monday morning");
    for (const f of ["DAILY", "WEEKLY", "NONE"] as const) {
      expect(digestScheduleLabel(f)).not.toMatch(/\d:\d\d/); // no promised send hour
    }
    expect(digestScheduleLabel("WEEKLY")).toMatch(/Monday/);
    expect(digestScheduleLabel("NONE")).toMatch(/off/);
  });
});

describe("/app overview page", () => {
  it("renders stats, the top five leads by score, and the super-admin right rail", async () => {
    render(await OverviewPage());
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Find the permits worth chasing.");
    expect(screen.getAllByText("$18.64M")).toHaveLength(2); // stat card + digest preview
    // Fixture totals: 25 leads, 6 scoring 90+, 9 filed in the last 3 days; the
    // 100-lead sample holds all 25, so page-derived numbers say "all 25".
    expect(screen.getByTestId("stat-recent")).toHaveTextContent("Filed in the last 3 days9of 25 leads in your markets");
    expect(screen.getByTestId("stat-hot")).toHaveTextContent("6");
    expect(screen.getByTestId("stat-avg")).toHaveTextContent("across all 25 leads");
    expect(screen.getByTestId("sparkline-scope")).toHaveTextContent("across all 25 leads");
    const rows = screen.getAllByTestId("score-badge").map((b) => b.textContent);
    expect(rows).toEqual(["100", "100", "100", "95", "90"]);
    expect(screen.getByRole("region", { name: "Source health" })).toBeInTheDocument();
    expect(screen.getByText("Next digest: tomorrow morning")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /permit filings per day/ })).toBeInTheDocument();
  });

  it("uses API totals for counts and labels sample-derived numbers when total > page size", async () => {
    const page = (total: number, items: LeadSummary[]) =>
      ({ items, total, page: 1, pageSize: items.length, freshness: { lastUpdatedAt: new Date().toISOString() } });
    const sample = [
      lead({ id: "a", score: 95, estimatedValue: 1_000_000, filedDate: new Date().toISOString() }),
      lead({ id: "b", score: 75, estimatedValue: 500_000 }),
    ];
    const spy = vi.spyOn(api, "getLeads").mockImplementation(async (q) => {
      if (q.minScore === 90) return page(40, sample.slice(0, 1));
      if (q.maxAgeDays === 3) return page(17, sample.slice(0, 1));
      return page(250, sample);
    });
    render(await OverviewPage());
    expect(spy).toHaveBeenCalledWith({ market: undefined, minScore: 90, pageSize: 1 }, "mock-token");
    expect(spy).toHaveBeenCalledWith({ market: undefined, maxAgeDays: 3, pageSize: 1 }, "mock-token");
    expect(screen.getByTestId("stat-hot")).toHaveTextContent("40");
    expect(screen.getByTestId("stat-recent")).toHaveTextContent("17of 250 leads in your markets");
    expect(screen.getByTestId("stat-avg")).toHaveTextContent("85across your top 2 leads");
    expect(screen.getByTestId("stat-value")).toHaveTextContent("$1.5MReported values across your top 2 leads");
    expect(screen.getByTestId("sparkline-scope")).toHaveTextContent("across your top 2 leads");
    const digest = screen.getByRole("region", { name: "Your email digest" });
    expect(digest).toHaveTextContent("Filed in the last 3 days17");
    expect(digest).toHaveTextContent("Hot leads40");
    expect(digest).toHaveTextContent("across your top 2 leads");
    spy.mockRestore();
  });

  it("scopes every count to the market in the URL", async () => {
    const spy = vi.spyOn(api, "getLeads");
    render(await OverviewPage({ searchParams: Promise.resolve({ market: "dallas-tx" }) }));
    for (const [q] of spy.mock.calls) expect(q.market).toBe("dallas-tx");
    expect(screen.getByTestId("stat-recent")).toHaveTextContent("of 8 leads in your markets");
    spy.mockRestore();
  });

  it("shows an explicit unavailable state when source health fails to load", async () => {
    const spy = vi.spyOn(api, "getAdminSources").mockRejectedValue(new api.ApiError("boom", 500));
    render(await OverviewPage());
    const panel = screen.getByRole("region", { name: "Source health" });
    expect(panel).toHaveTextContent("Source health is unavailable right now");
    expect(panel).not.toHaveTextContent("No sources configured yet.");
    spy.mockRestore();
  });

  it("hides source health from non-admin members", async () => {
    const spy = vi.spyOn(api, "getAccountMe").mockResolvedValue({ ...mockAccountMe, role: "MEMBER" });
    const sources = vi.spyOn(api, "getAdminSources");
    render(await OverviewPage());
    expect(screen.queryByRole("region", { name: "Source health" })).not.toBeInTheDocument();
    expect(sources).not.toHaveBeenCalled();
    spy.mockRestore();
    sources.mockRestore();
  });
});
