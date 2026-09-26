// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";

const redirect = vi.fn((path: string) => { throw new Error(`REDIRECT:${path}`); });
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  redirect: (p: string) => redirect(p),
  notFound: () => { throw new Error("NOT_FOUND"); },
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));

import LeadsPage from "@/app/app/leads/page";
import * as api from "@/lib/api";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

const renderPage = async (sp: Record<string, string>) =>
  render(await LeadsPage({ searchParams: Promise.resolve(sp) }));

describe("/app/leads page (mock API)", () => {
  it("renders every fixture lead with freshness and totals", async () => {
    await renderPage({});
    expect(screen.getByRole("heading", { name: "Leads" })).toBeInTheDocument();
    expect(screen.getByText(/Updated 12 minutes ago/)).toBeInTheDocument();
    expect(screen.getByText("Showing 1 to 25 of 25 results")).toBeInTheDocument();
    expect(within(screen.getByRole("table")).getAllByRole("link")).toHaveLength(25);
  });

  it("applies URL filters through lib/api", async () => {
    await renderPage({ minScore: "90", category: "FIRE_SPRINKLER" });
    const links = within(screen.getByRole("table")).getAllByRole("link");
    expect(links.map((l) => l.textContent)).toEqual([
      "Distribution Center — New Construction",
      "Warehouse Fire Sprinkler System",
      "Logistics Hub Fire Sprinkler Package",
    ]);
  });

  it("shows the search term and the empty state when nothing matches", async () => {
    await renderPage({ q: "zzz-no-such-lead" });
    expect(screen.getByText("“zzz-no-such-lead”")).toBeInTheDocument();
    expect(screen.getByText("No leads match these filters")).toBeInTheDocument();
    expect(screen.getByText("No results")).toBeInTheDocument();
  });

  it("sends an expired session back to /login", async () => {
    const spy = vi.spyOn(api, "getLeads").mockRejectedValueOnce(new api.ApiError("expired", 401));
    await expect(renderPage({})).rejects.toThrow("REDIRECT:/login");
    spy.mockRestore();
  });
});
