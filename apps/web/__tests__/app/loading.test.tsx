// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import AccountLoading from "@/app/app/account/loading";
import AlertsLoading from "@/app/app/alerts/loading";
import MarketsLoading from "@/app/app/markets/loading";
import AdminLoading from "@/app/app/admin/loading";

describe("per-route loading skeletons", () => {
  it.each([
    ["account", AccountLoading, "Loading account"],
    ["alerts", AlertsLoading, "Loading alert settings"],
    ["markets", MarketsLoading, "Loading markets"],
    ["admin", AdminLoading, "Loading admin data"],
  ])("%s has its own busy skeleton", (_route, Loading, label) => {
    const { container } = render(<Loading />);
    const root = screen.getByLabelText(label);
    expect(root).toHaveAttribute("aria-busy", "true");
    // Not the overview's stat-card skeleton.
    expect(container.querySelector(".xl\\:grid-cols-4")).toBeNull();
  });
});
