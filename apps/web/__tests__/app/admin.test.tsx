// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import * as api from "@/lib/api";
import { toast } from "sonner";
import AdminSourcesPage from "@/app/app/admin/sources/page";
import AdminRunsPage from "@/app/app/admin/runs/page";
import AdminUsersPage from "@/app/app/admin/users/page";
import AdminSubscriptionsPage from "@/app/app/admin/subscriptions/page";
import { SourceTable } from "@/components/app/admin/source-table";
import { formatDuration } from "@/components/app/admin/runs-table";
import { mockAccountMe } from "@/lib/fixtures/account";
import { mockAdminSources } from "@/lib/fixtures/admin";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));
beforeEach(() => vi.clearAllMocks());

describe("admin pages (super admin)", () => {
  it("lists every source with its health", async () => {
    render(await AdminSourcesPage());
    const table = screen.getByRole("table");
    expect(within(table).getAllByRole("row")).toHaveLength(6); // header + 5
    expect(within(table).getByText("City of Houston ePermits")).toBeInTheDocument();
    expect(within(table).getByText("Failed")).toBeInTheDocument();
  });

  it("filters runs by a known source and ignores unknown source ids", async () => {
    render(await AdminRunsPage({ searchParams: Promise.resolve({ sourceId: "src-004" }) }));
    expect(screen.getByText("3 runs")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "City of Dallas Permits" })).toHaveAttribute("aria-current", "page");
    expect(document.querySelectorAll("[data-flagged]")).toHaveLength(2);
  });

  it("falls back to all runs for an unknown source id", async () => {
    render(await AdminRunsPage({ searchParams: Promise.resolve({ sourceId: "nope" }) }));
    expect(screen.getByText("10 runs")).toBeInTheDocument();
  });

  it("renders the users and subscriptions hand-off pages", async () => {
    render(await AdminUsersPage());
    expect(screen.getByRole("link", { name: /Open Firebase console/ })).toHaveAttribute("target", "_blank");
    render(await AdminSubscriptionsPage());
    expect(screen.getByRole("link", { name: /Open Stripe dashboard/ })).toHaveAttribute("rel", "noopener noreferrer");
  });
});

describe("admin pages (non super admin)", () => {
  it.each([
    ["sources", () => AdminSourcesPage()],
    ["runs", () => AdminRunsPage({ searchParams: Promise.resolve({}) })],
    ["users", () => AdminUsersPage()],
    ["subscriptions", () => AdminSubscriptionsPage()],
  ])("redirects a MEMBER away from %s before loading admin data", async (_name, load) => {
    const me = vi.spyOn(api, "getAccountMe").mockResolvedValue({ ...mockAccountMe, role: "MEMBER" });
    const sources = vi.spyOn(api, "getAdminSources");
    await expect(load()).rejects.toThrow("REDIRECT:/app");
    expect(sources).not.toHaveBeenCalled();
    me.mockRestore();
    sources.mockRestore();
  });
});

describe("SourceTable", () => {
  it("toggles a source optimistically and reverts on failure", async () => {
    const spy = vi.spyOn(api, "setSourceActive").mockRejectedValueOnce(new Error("down"));
    render(<SourceTable sources={mockAdminSources.slice(0, 1)} />);
    fireEvent.click(screen.getByRole("button", { name: "Disable City of Houston ePermits" }));
    expect(screen.getByRole("button", { name: "Enable City of Houston ePermits" })).toBeInTheDocument();
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.getByRole("button", { name: "Disable City of Houston ePermits" })).toBeInTheDocument();
    expect(spy).toHaveBeenCalledWith("src-001", false, "mock-token");
    spy.mockRestore();
  });
});

describe("formatDuration", () => {
  it("renders seconds and minutes", () => {
    expect(formatDuration(42.7)).toBe("43s");
    expect(formatDuration(312.4)).toBe("5m 12s");
  });
});
