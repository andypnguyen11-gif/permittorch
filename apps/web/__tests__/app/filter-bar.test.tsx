// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { nextSearchFor } from "@/components/app/leads/filter-bar";

describe("FilterBar → LeadsQuery mapping", () => {
  it("maps score band choices to minScore and resets page", () => {
    expect(nextSearchFor({ page: 3 }, "score", "90")).toBe("?minScore=90");
    expect(nextSearchFor({}, "score", "80")).toBe("?minScore=80");
    expect(nextSearchFor({ minScore: 90 }, "score", "all")).toBe("");
  });

  it("maps age choices to maxAgeDays", () => {
    expect(nextSearchFor({}, "age", "1")).toBe("?maxAgeDays=1");
    expect(nextSearchFor({}, "age", "30")).toBe("?maxAgeDays=30");
    expect(nextSearchFor({ maxAgeDays: 7 }, "age", "all")).toBe("");
  });

  it("maps category and status, preserving other filters", () => {
    expect(nextSearchFor({ minScore: 80 }, "category", "FIRE_ALARM"))
      .toBe("?category=FIRE_ALARM&minScore=80");
    expect(nextSearchFor({ category: "FIRE_ALARM" }, "status", "FAILED"))
      .toBe("?category=FIRE_ALARM&status=FAILED");
    expect(nextSearchFor({ category: "FIRE_ALARM" }, "category", "all")).toBe("");
  });

  it("preserves q and market when changing filters", () => {
    expect(nextSearchFor({ q: "warehouse", market: "houston-tx" }, "score", "90"))
      .toBe("?market=houston-tx&minScore=90&q=warehouse");
  });
});

import "./dom-cleanup";
import { vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

import { FilterBar } from "@/components/app/leads/filter-bar";

describe("FilterBar", () => {
  it("renders labelled filter triggers showing the current selection", () => {
    render(<FilterBar query={{ category: "FIRE_ALARM", minScore: 90 }} />);
    expect(screen.getByRole("combobox", { name: /Category/ })).toHaveTextContent("Fire Alarm");
    expect(screen.getByRole("combobox", { name: /Score/ })).toHaveTextContent("90+");
    expect(screen.getByRole("combobox", { name: /Age/ })).toHaveTextContent("Any time");
    expect(screen.getByRole("combobox", { name: /Status/ })).toHaveTextContent("All");
  });

  it("offers Clear filters only when a filter is active, keeping market and search", () => {
    const { rerender } = render(<FilterBar query={{ market: "houston-tx" }} />);
    expect(screen.queryByRole("button", { name: "Clear filters" })).not.toBeInTheDocument();
    rerender(<FilterBar query={{ market: "houston-tx", q: "warehouse", status: "FAILED", page: 2 }} />);
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    expect(push).toHaveBeenCalledWith("/app/leads?market=houston-tx&q=warehouse");
  });
});
