// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen } from "@testing-library/react";
import type { AccountMe } from "@permittorch/types";

const router = { replace: vi.fn(), refresh: vi.fn(), push: vi.fn() };
vi.mock("next/navigation", () => ({ useRouter: () => router }));
vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()), getAccountMe: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("@/components/app/account/billing-buttons", () => ({
  BillingButtons: ({ plan }: { plan: unknown }) => <button>Manage billing{plan === null ? "" : " (plan)"}</button>,
}));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { getAccountMe } from "@/lib/api";
import { CheckoutConfirming, CONFIRM_POLL_MS, CONFIRM_TIMEOUT_MS } from "@/components/app/account/checkout-confirming";

const me = (plan: AccountMe["plan"]): AccountMe => ({
  id: "u1", email: "a@b.c", role: "MEMBER", organizationName: "Org", plan,
  digestFrequency: "NONE", hasLiveSubscription: plan !== null,
});

// Advances fake time step by step so each awaited poll resolves before the next timer.
async function advance(ms: number) {
  for (let t = 0; t < ms; t += 500) await act(() => vi.advanceTimersByTimeAsync(500));
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.clearAllMocks();
});

describe("CheckoutConfirming", () => {
  it("polls every 2s up to 30s by default", () => {
    expect(CONFIRM_POLL_MS).toBe(2_000);
    expect(CONFIRM_TIMEOUT_MS).toBe(30_000);
  });

  it("shows the confirming state and swaps to the account view once a plan appears", async () => {
    vi.mocked(getAccountMe)
      .mockResolvedValueOnce(me(null))
      .mockResolvedValueOnce(me(null))
      .mockResolvedValue(me("PRO"));
    render(<CheckoutConfirming />);
    expect(screen.getByText("Confirming your subscription…")).toBeInTheDocument();

    await advance(4_500);

    expect(getAccountMe).toHaveBeenCalledTimes(3);
    expect(router.replace).toHaveBeenCalledWith("/app/account");
    expect(router.refresh).toHaveBeenCalled();
  });

  it("stops after the timeout with a refresh hint and retries on demand", async () => {
    vi.mocked(getAccountMe).mockResolvedValue(me(null));
    render(<CheckoutConfirming />);

    await advance(CONFIRM_TIMEOUT_MS + 2_000);
    expect(screen.getByText(/still confirming your subscription — refresh in a minute/)).toBeInTheDocument();
    const calls = vi.mocked(getAccountMe).mock.calls.length;
    expect(calls).toBeGreaterThanOrEqual(14);
    expect(calls).toBeLessThanOrEqual(16);
    await advance(10_000);
    expect(getAccountMe).toHaveBeenCalledTimes(calls);   // no polling after the timeout
    expect(router.replace).not.toHaveBeenCalled();

    vi.mocked(getAccountMe).mockResolvedValue(me("STARTER"));
    fireEvent.click(screen.getByRole("button", { name: "Check again" }));
    await advance(1_000);
    expect(router.replace).toHaveBeenCalledWith("/app/account");
  });

  it("ends on a live subscription without a plan and offers the billing portal", async () => {
    vi.mocked(getAccountMe)
      .mockResolvedValueOnce(me(null))
      .mockResolvedValue({ ...me(null), hasLiveSubscription: true });
    render(<CheckoutConfirming />);
    await advance(2_500);
    expect(screen.getByText("Your subscription needs attention — manage billing.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Manage billing" })).toBeInTheDocument();
    expect(screen.queryByText("Confirming your subscription…")).not.toBeInTheDocument();
    const calls = vi.mocked(getAccountMe).mock.calls.length;
    await advance(10_000);
    expect(getAccountMe).toHaveBeenCalledTimes(calls);   // polling stopped
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("keeps polling through transient API errors", async () => {
    vi.mocked(getAccountMe)
      .mockRejectedValueOnce(new Error("network"))
      .mockResolvedValue(me("PRO"));
    render(<CheckoutConfirming />);
    await advance(2_500);
    expect(router.replace).toHaveBeenCalledWith("/app/account");
  });
});
