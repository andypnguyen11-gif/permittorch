// @vitest-environment jsdom
import "./dom-cleanup";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

const { redirect, getApiToken } = vi.hoisted(() => ({
  redirect: vi.fn((p: string) => { throw new Error(`REDIRECT:${p}`); }),
  getApiToken: vi.fn(async () => "mock-token"),
}));
vi.mock("next/navigation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("next/navigation")>()),
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
  usePathname: () => "/app",
  useSearchParams: () => new URLSearchParams(),
  redirect,
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: () => getApiToken() }));

import AppLayout from "@/app/app/layout";
import * as api from "@/lib/api";
import { sessionExpiredError } from "@/components/app/session-digest";
import { mockAccountMe } from "@/lib/fixtures/account";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));
afterEach(() => vi.restoreAllMocks());

const renderLayout = async () =>
  render(await AppLayout({ children: <p>page content</p> }));

describe("/app layout failure handling", () => {
  it("renders the app shell and the page when the account loads", async () => {
    await renderLayout();
    expect(screen.getByText("page content")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Account menu" })).toBeInTheDocument();
  });

  it("shows the member view in mock mode when NEXT_PUBLIC_MOCK_ROLE=MEMBER", async () => {
    vi.stubEnv("NEXT_PUBLIC_MOCK_ROLE", "MEMBER");
    await renderLayout();
    expect(screen.queryByRole("link", { name: "Sources" })).not.toBeInTheDocument();
    vi.stubEnv("NEXT_PUBLIC_MOCK_ROLE", "SUPER_ADMIN");
    await renderLayout();
    expect(screen.getAllByRole("link", { name: "Sources" }).length).toBeGreaterThan(0);
    vi.stubEnv("NEXT_PUBLIC_MOCK_ROLE", "");
  });

  it("renders the session-recovery state on an API 401, without redirecting", async () => {
    vi.spyOn(api, "getAccountMe").mockRejectedValue(new api.ApiError("Unauthorized", 401));
    await renderLayout();
    expect(screen.getByText("We couldn’t verify your session")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sign in again" })).toBeInTheDocument();
    expect(screen.queryByText("page content")).not.toBeInTheDocument();
    expect(redirect).not.toHaveBeenCalled();
  });

  it("renders the session-recovery state when the session cookie cannot be read", async () => {
    getApiToken.mockRejectedValueOnce(sessionExpiredError());
    await renderLayout();
    expect(screen.getByText("We couldn’t verify your session")).toBeInTheDocument();
    expect(redirect).not.toHaveBeenCalled();
  });

  it("renders an inline error shell with retry on other API failures", async () => {
    vi.spyOn(api, "getAccountMarkets").mockRejectedValue(new api.ApiError("boom", 500));
    await renderLayout();
    expect(screen.getByRole("alert")).toHaveTextContent("Something went wrong loading this page");
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
    expect(screen.queryByText("page content")).not.toBeInTheDocument();
    expect(redirect).not.toHaveBeenCalled();
  });

  it("shows the agreement screen, and no page, until the user agrees to the terms", async () => {
    vi.spyOn(api, "getAccountMe").mockResolvedValue({ ...mockAccountMe, termsAccepted: false });
    await renderLayout();
    expect(screen.getByRole("heading", { level: 1, name: "Agree to the terms to see your leads" })).toBeInTheDocument();
    expect(screen.queryByText("page content")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Account menu" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Leads" })).not.toBeInTheDocument();
  });

  it("renders the error shell when the API is unreachable", async () => {
    vi.spyOn(api, "getAccountMe").mockRejectedValue(new TypeError("fetch failed"));
    await renderLayout();
    expect(screen.getByRole("alert")).toHaveTextContent("Something went wrong loading this page");
  });
});
