// @vitest-environment jsdom
// Server-component page smoke tests against the mock API (lib/api.ts → fixtures).
import "./dom-cleanup";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => "/app",
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
  notFound: () => { throw new Error("NOT_FOUND"); },
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import SavedPage from "@/app/app/saved/page";
import AlertsPage from "@/app/app/alerts/page";
import AccountPage from "@/app/app/account/page";
import MarketsPage from "@/app/app/markets/page";
import { orderMarkets } from "@/components/app/order-markets";
import * as api from "@/lib/api";
import { TooltipProvider } from "@/components/ui/tooltip";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

describe("/app/saved", () => {
  it("lists the saved fixture leads with their tracking status", async () => {
    render(await SavedPage());
    expect(screen.getByRole("heading", { name: "Saved leads" })).toBeInTheDocument();
    expect(screen.getByText("Warehouse Fire Sprinkler System")).toBeInTheDocument();
    expect(screen.getByText("Restaurant Kitchen Hood Suppression")).toBeInTheDocument();
    expect(screen.getByText("Mixed-Use Tower Fire Alarm")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Mark contacted" })).toHaveLength(2);
    expect(screen.getAllByRole("button", { name: "Mark saved" })).toHaveLength(1);
  });
});

describe("/app/alerts", () => {
  it("shows the account's digest frequency", async () => {
    render(await AlertsPage());
    expect(screen.getByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: /Daily/ })).toBeChecked();
    expect(screen.getByText("john@davisfireprotection.com")).toBeInTheDocument();
  });
});

describe("/app/account", () => {
  it("shows profile, plan, and entitled markets", async () => {
    render(<TooltipProvider>{await AccountPage()}</TooltipProvider>);
    expect(screen.getByText("Davis Fire Protection")).toBeInTheDocument();
    expect(screen.getByText("Pro plan")).toBeInTheDocument();
    expect(screen.getByText("Houston")).toBeInTheDocument();
    expect(screen.getByText("Dallas")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Upgrade to Territory" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: /Subscribe/ })).not.toBeInTheDocument();
  });

  it("offers the plan + market picker without a plan, preselecting ?plan= and listing the catalog", async () => {
    const me = vi.spyOn(api, "getAccountMe").mockResolvedValue({
      email: "new@example.com", role: "MEMBER", organizationName: "New Co", plan: null, digestFrequency: "NONE",
    });
    const entitled = vi.spyOn(api, "getAccountMarkets").mockResolvedValue([]);
    render(<TooltipProvider>{await AccountPage({ searchParams: Promise.resolve({ plan: "territory" }) })}</TooltipProvider>);
    expect(screen.getByText("No active plan")).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Territory" })).toBeChecked();
    for (const name of ["Houston", "Dallas", "Austin"])
      expect(screen.getByRole("checkbox", { name })).toBeInTheDocument();
    me.mockRestore();
    entitled.mockRestore();
  });

  it("falls back to Pro for an invalid ?plan=", async () => {
    const me = vi.spyOn(api, "getAccountMe").mockResolvedValue({
      email: "new@example.com", role: "MEMBER", organizationName: "New Co", plan: null, digestFrequency: "NONE",
    });
    render(<TooltipProvider>{await AccountPage({ searchParams: Promise.resolve({ plan: "GOLD" }) })}</TooltipProvider>);
    expect(screen.getByRole("radio", { name: "Pro" })).toBeChecked();
    me.mockRestore();
  });
});

const market = (slug: string, name: string, city: string) => ({ id: slug, slug, name, city, state: "TX" });

describe("/app/markets", () => {
  it("orders entitled markets first without duplicates", () => {
    const h = market("houston-tx", "Houston, TX", "Houston");
    const a = market("austin-tx", "Austin, TX", "Austin");
    expect(orderMarkets([a, h], [h]).map((m) => m.slug)).toEqual(["houston-tx", "austin-tx"]);
  });

  it("shows lead counts for entitled markets and an upgrade path for locked ones", async () => {
    const spy = vi.spyOn(api, "getMarkets").mockResolvedValue([
      market("houston-tx", "Houston", "Houston"),
      market("dallas-tx", "Dallas", "Dallas"),
      market("austin-tx", "Austin", "Austin"),
    ]);
    render(await MarketsPage());
    const houston = screen.getByRole("heading", { name: "Houston" }).closest("li")!;
    expect(houston).toHaveTextContent("17Leads");
    expect(houston).not.toHaveTextContent(/open leads/i); // counts include closed permits
    expect(screen.getByRole("heading", { name: "Dallas" }).closest("li")).toHaveTextContent("8Leads");
    expect(screen.getAllByRole("link", { name: /View leads/ })[0])
      .toHaveAttribute("href", "/app/leads?market=houston-tx");
    const austin = screen.getByRole("heading", { name: "Austin" }).closest("li")!;
    expect(austin).toHaveTextContent("Locked");
    expect(screen.getByRole("link", { name: "Upgrade to unlock" })).toHaveAttribute("href", "/app/account");
    spy.mockRestore();
  });
});
