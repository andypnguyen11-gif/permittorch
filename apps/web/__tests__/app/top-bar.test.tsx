// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

const push = vi.fn();
let pathname = "/app";
let search = "";
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  usePathname: () => pathname,
  useSearchParams: () => new URLSearchParams(search),
}));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
vi.mock("firebase/auth", () => ({ signOut: vi.fn().mockResolvedValue(undefined) }));

import { signOut } from "firebase/auth";
import { TopBar } from "@/components/app/top-bar";

const markets = [
  { id: "m1", name: "Houston, TX", city: "Houston", state: "TX", slug: "houston-tx" },
];

beforeEach(() => {
  vi.clearAllMocks();
  pathname = "/app";
  search = "";
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
});

describe("TopBar", () => {
  it("focuses the search input on Cmd+K / Ctrl+K", () => {
    render(<TopBar markets={markets} email="john@davisfire.com" />);
    const input = screen.getByRole("searchbox", { name: "Search leads" });
    expect(input).not.toHaveFocus();
    fireEvent.keyDown(window, { key: "k", metaKey: true });
    expect(input).toHaveFocus();
    (input as HTMLInputElement).blur();
    fireEvent.keyDown(window, { key: "K", ctrlKey: true });
    expect(input).toHaveFocus();
  });

  it("submits the search to the leads page with an encoded q", () => {
    render(<TopBar markets={markets} email="john@davisfire.com" />);
    const input = screen.getByRole("searchbox", { name: "Search leads" });
    fireEvent.change(input, { target: { value: "smoke & fire" } });
    fireEvent.submit(input.closest("form")!);
    expect(push).toHaveBeenCalledWith("/app/leads?q=smoke%20%26%20fire");
  });

  it("prefills the search from the current leads query", () => {
    pathname = "/app/leads";
    search = "q=warehouse";
    render(<TopBar markets={markets} email="john@davisfire.com" />);
    expect(screen.getByRole("searchbox", { name: "Search leads" })).toHaveValue("warehouse");
  });

  it("clears the session cookie first, then Firebase, then replaces the page with /login", async () => {
    const calls: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (url: string) => { calls.push(`fetch ${url}`); return { ok: true }; }));
    vi.mocked(signOut).mockImplementation(async () => { calls.push("firebase signOut"); });
    const replace = vi.fn((url: string) => { calls.push(`replace ${url}`); });
    vi.stubGlobal("location", { ...window.location, replace });

    render(<TopBar markets={markets} email="john.davis@davisfire.com" />);
    const trigger = screen.getByRole("button", { name: "Account menu" });
    expect(trigger).toHaveTextContent("JD");
    fireEvent.click(trigger);
    fireEvent.click(await screen.findByRole("menuitem", { name: /Sign out/ }));
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(calls).toEqual(["fetch /api/logout", "firebase signOut", "replace /login"]);
    expect(push).not.toHaveBeenCalledWith("/login");
    vi.unstubAllGlobals();
  });

  it("stays signed in to Firebase when clearing the session cookie fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 500 }));
    const replace = vi.fn();
    vi.stubGlobal("location", { ...window.location, replace });
    render(<TopBar markets={markets} email="john.davis@davisfire.com" />);
    fireEvent.click(screen.getByRole("button", { name: "Account menu" }));
    fireEvent.click(await screen.findByRole("menuitem", { name: /Sign out/ }));
    await waitFor(() => expect(fetch).toHaveBeenCalledWith("/api/logout"));
    await Promise.resolve();
    expect(signOut).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });
});
