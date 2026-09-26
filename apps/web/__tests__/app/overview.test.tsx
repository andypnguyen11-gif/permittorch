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
import { computeOverviewStats } from "@/components/app/overview/stat-cards";
import { dailyCounts } from "@/components/app/overview/activity-sparkline";
import { digestScheduleLabel } from "@/components/app/overview/digest-preview";
import { mockLeads } from "@/lib/fixtures/leads";
import { mockAccountMe } from "@/lib/fixtures/account";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

const DAY = 86_400_000;
const lead = (o: Partial<LeadSummary>): LeadSummary => ({
  id: "x", score: 50, title: "t", address: null, city: "Houston", state: "TX",
  category: "FIRE_ALARM", permitType: null, status: "NEW", filedDate: null,
  estimatedValue: null, reason: "r", isNew: false, ...o,
});

describe("computeOverviewStats", () => {
  it("summarises the fixture leads", () => {
    expect(computeOverviewStats(mockLeads)).toEqual({
      newOpportunities: 9, hotLeads: 3, avgScore: 73, totalValue: 18_635_000,
    });
  });
  it("handles no leads without dividing by zero", () => {
    expect(computeOverviewStats([])).toEqual({ newOpportunities: 0, hotLeads: 0, avgScore: 0, totalValue: 0 });
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
    expect(digestScheduleLabel("DAILY")).toMatch(/tomorrow/);
    expect(digestScheduleLabel("WEEKLY")).toMatch(/Monday/);
    expect(digestScheduleLabel("NONE")).toMatch(/off/);
  });
});

describe("/app overview page", () => {
  it("renders stats, the top five leads by score, and the super-admin right rail", async () => {
    render(await OverviewPage());
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Find the permits worth chasing.");
    expect(screen.getAllByText("$18.64M")).toHaveLength(2); // stat card + digest preview
    const rows = screen.getAllByTestId("score-badge").map((b) => b.textContent);
    expect(rows).toEqual(["94", "92", "91", "89", "87"]);
    expect(screen.getByRole("region", { name: "Source health" })).toBeInTheDocument();
    expect(screen.getByText("Next digest tomorrow at 6:00 AM")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /permit filings per day/ })).toBeInTheDocument();
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
