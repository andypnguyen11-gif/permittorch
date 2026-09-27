// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

// Call sites of the locked PostHog events (PRD §49). The wrapper itself is covered in analytics.test.ts.
vi.mock("@/lib/analytics", () => ({ track: vi.fn(), initAnalytics: vi.fn() }));
vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()),
  saveLead: vi.fn(), unsaveLead: vi.fn(), updateEmailPreferences: vi.fn(), createCheckout: vi.fn(),
}));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));
const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  usePathname: () => "/app/leads",
  useSearchParams: () => new URLSearchParams(""),
}));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
vi.mock("firebase/auth", () => ({ signOut: vi.fn().mockResolvedValue(undefined) }));

import { track, initAnalytics } from "@/lib/analytics";
import { createCheckout, saveLead, updateEmailPreferences } from "@/lib/api";
import { SaveButton } from "@/components/app/lead-detail/save-button";
import { DigestForm } from "@/components/app/alerts/digest-form";
import { TopBar } from "@/components/app/top-bar";
import { FilterBar } from "@/components/app/leads/filter-bar";
import { CheckoutPicker } from "@/components/app/account/billing-buttons";
import { TrackOnMount } from "@/components/app/track-on-mount";
import { TooltipProvider } from "@/components/ui/tooltip";

beforeEach(() => {
  vi.clearAllMocks();
  vi.unstubAllEnvs();
});

async function pickOption(trigger: HTMLElement, name: string) {
  fireEvent.click(trigger);
  const option = await screen.findByRole("option", { name });
  // Base UI commits a selection on pointer-up/click of a highlighted item.
  fireEvent.pointerMove(option);
  fireEvent.mouseMove(option);
  fireEvent.pointerDown(option);
  fireEvent.mouseDown(option);
  fireEvent.pointerUp(option);
  fireEvent.mouseUp(option);
  fireEvent.click(option);
}

describe("analytics call sites", () => {
  it("lead_saved fires after a successful save only", async () => {
    vi.mocked(saveLead).mockResolvedValueOnce({ id: "sl_1" } as never);
    render(<SaveButton leadId="op_1" savedId={null} />);
    fireEvent.click(screen.getByRole("button", { name: "Save lead" }));
    await waitFor(() => expect(track).toHaveBeenCalledWith("lead_saved", { leadId: "op_1" }));

    vi.mocked(track).mockClear();
    vi.mocked(saveLead).mockRejectedValueOnce(new Error("boom"));
    render(<SaveButton leadId="op_2" savedId={null} />);
    fireEvent.click(screen.getAllByRole("button", { name: "Save lead" }).at(-1)!);
    await waitFor(() => expect(saveLead).toHaveBeenCalledTimes(2));
    expect(track).not.toHaveBeenCalled();
  });

  it("digest_enabled fires for DAILY/WEEKLY, never for turning the digest off", async () => {
    vi.mocked(updateEmailPreferences).mockResolvedValue(undefined);
    const { unmount } = render(<DigestForm initialFrequency="NONE" />);
    fireEvent.click(screen.getByRole("radio", { name: /Weekly/ }));
    await waitFor(() => expect(track).toHaveBeenCalledWith("digest_enabled", { frequency: "WEEKLY" }));
    unmount();

    vi.mocked(track).mockClear();
    render(<DigestForm initialFrequency="DAILY" />);
    fireEvent.click(screen.getByRole("radio", { name: /Off/ }));
    await waitFor(() => expect(updateEmailPreferences).toHaveBeenLastCalledWith("NONE", "mock-token"));
    expect(track).not.toHaveBeenCalled();
  });

  it("search_performed sends only the trimmed query's length, never the text; an empty search is not tracked", () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
    render(<TopBar markets={[]} email="a@b.test" />);
    const input = screen.getByRole("searchbox", { name: "Search leads" });
    fireEvent.change(input, { target: { value: "  warehouse  " } });
    fireEvent.submit(input.closest("form")!);
    expect(track).toHaveBeenCalledExactlyOnceWith("search_performed", { queryLength: 9 });
    fireEvent.change(input, { target: { value: "   " } });
    fireEvent.submit(input.closest("form")!);
    expect(track).toHaveBeenCalledTimes(1);
  });

  it("filter_changed fires for a filter-bar choice", async () => {
    render(<FilterBar query={{}} />);
    await pickOption(screen.getByRole("combobox", { name: /Score/ }), "80+");
    await waitFor(() => expect(track).toHaveBeenCalledWith("filter_changed", { filter: "score", value: "80" }));
    expect(push).toHaveBeenCalledWith("/app/leads?minScore=80");
  });

  it("filter_changed fires for a market switch", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
    const markets = [
      { id: "m1", name: "Austin", city: "Austin", state: "TX", slug: "austin-tx" },
      { id: "m2", name: "Fort Worth", city: "Fort Worth", state: "TX", slug: "fort-worth-tx" },
    ];
    render(<TopBar markets={markets} email="a@b.test" />);
    await pickOption(screen.getAllByRole("combobox", { name: "Market" })[0], "Fort Worth");
    await waitFor(() =>
      expect(track).toHaveBeenCalledWith("filter_changed", { filter: "market", value: "fort-worth-tx" }));
  });

  it("checkout_started fires with the plan once the checkout session exists", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createCheckout).mockResolvedValue({ url: "#" });
    const markets = [{ id: "m1", name: "Austin", city: "Austin", state: "TX", slug: "austin-tx" }];
    render(<TooltipProvider><CheckoutPicker markets={markets} initialPlan="PRO" /></TooltipProvider>);
    fireEvent.click(screen.getByRole("radio", { name: "Austin" }));
    fireEvent.click(screen.getByRole("button", { name: "Subscribe to Pro" }));
    await waitFor(() => expect(track).toHaveBeenCalledWith("checkout_started", { plan: "PRO" }));
  });

  it("checkout_started is not tracked when creating the session fails", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createCheckout).mockRejectedValue(new Error("boom"));
    const markets = [{ id: "m1", name: "Austin", city: "Austin", state: "TX", slug: "austin-tx" }];
    render(<TooltipProvider><CheckoutPicker markets={markets} initialPlan="PRO" /></TooltipProvider>);
    fireEvent.click(screen.getByRole("radio", { name: "Austin" }));
    fireEvent.click(screen.getByRole("button", { name: "Subscribe to Pro" }));
    await waitFor(() => expect(createCheckout).toHaveBeenCalled());
    expect(track).not.toHaveBeenCalled();
  });

  it("TrackOnMount initialises analytics and fires its event once", () => {
    const props = { leadId: "op_1", score: 88, category: "FIRE_ALARM" };
    const { rerender } = render(<TrackOnMount event="lead_opened" props={props} />);
    rerender(<TrackOnMount event="lead_opened" props={{ ...props }} />);
    expect(initAnalytics).toHaveBeenCalled();
    expect(track).toHaveBeenCalledExactlyOnceWith("lead_opened", props);
  });
});
