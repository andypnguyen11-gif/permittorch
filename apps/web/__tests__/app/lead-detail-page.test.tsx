// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn() }),
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
  notFound: () => { throw new Error("NOT_FOUND"); },
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import LeadDetailPage from "@/app/app/leads/[id]/page";
import * as api from "@/lib/api";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

const renderPage = async (id: string) =>
  render(await LeadDetailPage({ params: Promise.resolve({ id }) }));

describe("/app/leads/[id] page (mock API)", () => {
  it("renders the curated lead with its full score explanation and source link", async () => {
    await renderPage("lead-001");
    expect(screen.getByRole("heading", { level: 1, name: "Warehouse Fire Sprinkler System" })).toBeInTheDocument();
    expect(screen.getByText("Why this is a 94")).toBeInTheDocument();
    expect(screen.getAllByTestId("signal-weight")).toHaveLength(6);
    expect(screen.getByText("25-176389")).toBeInTheDocument();
    const source = screen.getByRole("link", { name: /View original record/ });
    expect(source).toHaveAttribute("target", "_blank");
    expect(source).toHaveAttribute("rel", "noopener noreferrer");
    // Saved in fixtures → button starts in the saved state.
    expect(screen.getByRole("button", { name: "Saved" })).toBeInTheDocument();
  });

  it("is null-safe: missing owner/contractor render as em dashes and negative signals are shown", async () => {
    await renderPage("lead-022");
    const permit = screen.getByRole("region", { name: "Permit" });
    expect(within(permit).getAllByText("—").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("−20")).toBeInTheDocument();
    expect(screen.getByText("No participants listed on this permit.")).toBeInTheDocument();
  });

  it("maps a 404 from the API to the not-found page", async () => {
    const spy = vi.spyOn(api, "getLead").mockRejectedValueOnce(new api.ApiError("nope", 404));
    await expect(renderPage("lead-404")).rejects.toThrow("NOT_FOUND");
    spy.mockRestore();
  });

  it("sends an expired session back to /login", async () => {
    const spy = vi.spyOn(api, "getLead").mockRejectedValueOnce(new api.ApiError("expired", 401));
    await expect(renderPage("lead-001")).rejects.toThrow("REDIRECT:/login");
    spy.mockRestore();
  });
});
