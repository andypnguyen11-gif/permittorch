// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";

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
import { leadsHrefWith } from "@/components/app/market-select";

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
    expect(push).toHaveBeenCalledWith("/app/leads?q=smoke+%26+fire");
  });

  it("merges a search into the current leads filters and resets the page", () => {
    pathname = "/app/leads";
    search = "market=houston-tx&category=FIRE_ALARM&minScore=80&page=3&q=old";
    render(<TopBar markets={markets} email="john@davisfire.com" />);
    const input = screen.getByRole("searchbox", { name: "Search leads" });
    fireEvent.change(input, { target: { value: "pump" } });
    fireEvent.submit(input.closest("form")!);
    expect(push).toHaveBeenCalledWith("/app/leads?market=houston-tx&category=FIRE_ALARM&minScore=80&q=pump");
    fireEvent.change(input, { target: { value: "  " } });
    fireEvent.submit(input.closest("form")!);
    expect(push).toHaveBeenLastCalledWith("/app/leads?market=houston-tx&category=FIRE_ALARM&minScore=80");
  });

  it("switching market from the selector keeps q and the other filters", async () => {
    pathname = "/app/leads";
    search = "q=warehouse&category=FIRE_SPRINKLER&page=2";
    const two = [...markets, { id: "m2", name: "Dallas, TX", city: "Dallas", state: "TX", slug: "dallas-tx" }];
    render(<TopBar markets={two} email="john@davisfire.com" />);
    const [trigger] = screen.getAllByRole("combobox", { name: "Market" });
    fireEvent.click(trigger);
    const option = await screen.findByRole("option", { name: "Dallas, TX" });
    // Base UI commits a selection on pointer-up/click of a highlighted item.
    fireEvent.pointerMove(option);
    fireEvent.mouseMove(option);
    fireEvent.pointerDown(option);
    fireEvent.mouseDown(option);
    fireEvent.pointerUp(option);
    fireEvent.mouseUp(option);
    fireEvent.click(option);
    await waitFor(() =>
      expect(push).toHaveBeenCalledWith("/app/leads?market=dallas-tx&category=FIRE_SPRINKLER&q=warehouse"));
  });

  it("offers the market selector inside the mobile navigation sheet", async () => {
    render(<TopBar markets={markets} email="john@davisfire.com" />);
    fireEvent.click(screen.getByRole("button", { name: "Open navigation" }));
    const sheet = await screen.findByRole("dialog");
    expect(within(sheet).getByRole("combobox", { name: "Market" })).toBeInTheDocument();
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

describe("leadsHrefWith", () => {
  const sp = (s: string) => new URLSearchParams(s);
  it("keeps q and other filters when the market changes on /app/leads", () => {
    expect(leadsHrefWith("/app/leads", sp("q=warehouse&category=FIRE_ALARM&page=2"), { market: "dallas-tx" }))
      .toBe("/app/leads?market=dallas-tx&category=FIRE_ALARM&q=warehouse");
  });
  it("drops the market for 'All my markets'", () => {
    expect(leadsHrefWith("/app/leads", sp("market=houston-tx&q=pump"), { market: undefined }))
      .toBe("/app/leads?q=pump");
  });
  it("starts from a clean query outside /app/leads", () => {
    expect(leadsHrefWith("/app/saved", sp("foo=bar"), { market: "houston-tx" }))
      .toBe("/app/leads?market=houston-tx");
  });
  it("drops invalid filters from the URL instead of forwarding them", () => {
    expect(leadsHrefWith("/app/leads", sp("minScore=999&category=NOPE"), { q: "x" }))
      .toBe("/app/leads?q=x");
  });
});
