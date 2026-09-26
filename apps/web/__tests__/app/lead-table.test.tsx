// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import type { LeadSummary } from "@permittorch/types";
import { LeadTable } from "@/components/app/leads/lead-table";
import { FreshnessLine } from "@/components/app/leads/freshness-line";

const HOURS = 3_600_000;
const lead = (overrides: Partial<LeadSummary>): LeadSummary => ({
  id: "lead-x", score: 92, title: "Warehouse Fire Sprinkler System",
  address: "8811 Katy Fwy", city: "Houston", state: "TX",
  category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
  filedDate: new Date(Date.now() - 48 * HOURS).toISOString(),
  estimatedValue: 1850000,
  reason: "Large commercial build-out in west Houston.", isNew: true,
  ...overrides,
});

describe("LeadTable", () => {
  it("renders score badge, title, address, relative date, value, and reason", () => {
    render(<LeadTable leads={[lead({})]} />);
    expect(screen.getByTestId("score-badge")).toHaveTextContent("92");
    expect(screen.getByText("Warehouse Fire Sprinkler System")).toBeInTheDocument();
    expect(screen.getByText(/8811 Katy Fwy/)).toBeInTheDocument();
    expect(screen.getByText("2 days ago")).toBeInTheDocument();
    expect(screen.getByText("$1.85M")).toBeInTheDocument();
    expect(screen.getByText("Large commercial build-out in west Houston.")).toBeInTheDocument();
  });

  it("shows a New badge only when isNew", () => {
    const { rerender } = render(<LeadTable leads={[lead({ isNew: true })]} />);
    expect(screen.getByText("New")).toBeInTheDocument();
    rerender(<LeadTable leads={[lead({ isNew: false, status: "ACTIVE" })]} />);
    expect(screen.queryByText("New")).not.toBeInTheDocument();
  });

  it("renders em dashes for null value and links the row to the detail page", () => {
    render(<LeadTable leads={[lead({ estimatedValue: null, id: "lead-9" })]} />);
    expect(screen.getByText("—")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Warehouse Fire Sprinkler System/ }))
      .toHaveAttribute("href", "/app/leads/lead-9");
  });

  it("renders the empty state when no leads match", () => {
    render(<LeadTable leads={[]} />);
    expect(screen.getByText("No leads match these filters")).toBeInTheDocument();
  });
});

describe("FreshnessLine", () => {
  it("renders honest freshness and an unknown state", () => {
    const { rerender } = render(
      <FreshnessLine freshness={{ lastUpdatedAt: new Date(Date.now() - 12 * 60_000).toISOString() }} />,
    );
    expect(screen.getByText("Updated 12 minutes ago")).toBeInTheDocument();
    rerender(<FreshnessLine freshness={{ lastUpdatedAt: null }} />);
    expect(screen.getByText("Freshness unknown")).toBeInTheDocument();
  });
});

import { LeadsPagination, pageWindow } from "@/components/app/leads/pagination";

describe("FreshnessLine staleness", () => {
  it("flags data older than a day as possibly stale instead of presenting it as current", () => {
    render(<FreshnessLine freshness={{ lastUpdatedAt: new Date(Date.now() - 3 * 24 * HOURS).toISOString() }} />);
    expect(screen.getByText(/Updated 3 days ago/)).toBeInTheDocument();
    expect(screen.getByText(/may be stale/)).toBeInTheDocument();
  });
});

describe("pageWindow", () => {
  it("returns every page when there are few", () => {
    expect(pageWindow(1, 3)).toEqual([1, 2, 3]);
  });
  it("collapses distant pages into gaps around the current page", () => {
    expect(pageWindow(1, 26)).toEqual([1, 2, "gap", 26]);
    expect(pageWindow(10, 26)).toEqual([1, "gap", 9, 10, 11, "gap", 26]);
    expect(pageWindow(26, 26)).toEqual([1, "gap", 25, 26]);
  });
});

describe("LeadsPagination", () => {
  it("summarises the range and disables prev on the first page", () => {
    render(<LeadsPagination query={{ category: "FIRE_ALARM" }} total={60} />);
    expect(screen.getByText("Showing 1 to 25 of 60 results")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Previous page" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Next page" }))
      .toHaveAttribute("href", "/app/leads?category=FIRE_ALARM&page=2");
    expect(screen.getByRole("link", { name: "Page 3" }))
      .toHaveAttribute("href", "/app/leads?category=FIRE_ALARM&page=3");
    expect(screen.getByText("1")).toHaveAttribute("aria-current", "page");
  });

  it("links page 1 without a page param and handles an empty result", () => {
    const { rerender } = render(<LeadsPagination query={{ page: 3 }} total={60} />);
    expect(screen.getByRole("link", { name: "Previous page" })).toHaveAttribute("href", "/app/leads?page=2");
    expect(screen.getByRole("link", { name: "Page 1" })).toHaveAttribute("href", "/app/leads");
    rerender(<LeadsPagination query={{}} total={0} />);
    expect(screen.getByText("No results")).toBeInTheDocument();
  });
});
