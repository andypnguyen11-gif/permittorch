// @vitest-environment jsdom
import "./dom-cleanup";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("@/lib/api", () => ({ createBillingPortal: vi.fn(), createCheckout: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { createBillingPortal, createCheckout } from "@/lib/api";
import { toast } from "sonner";
import { BillingButtons, nextPlan } from "@/components/app/account/billing-buttons";
import { TooltipProvider } from "@/components/ui/tooltip";

const renderButtons = (plan: Parameters<typeof BillingButtons>[0]["plan"]) =>
  render(<TooltipProvider><BillingButtons plan={plan} /></TooltipProvider>);

beforeEach(() => vi.clearAllMocks());
afterEach(() => vi.unstubAllEnvs());

describe("nextPlan", () => {
  it("steps up one tier and stops at Territory", () => {
    expect(nextPlan(null)).toBe("PRO");
    expect(nextPlan("STARTER")).toBe("PRO");
    expect(nextPlan("PRO")).toBe("TERRITORY");
    expect(nextPlan("TERRITORY")).toBeNull();
  });
});

describe("BillingButtons", () => {
  it("disables billing in mock mode instead of rendering dead buttons", () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1");
    renderButtons("PRO");
    expect(screen.getByRole("button", { name: "Manage billing" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Upgrade to Territory" })).toBeDisabled();
  });

  it("opens checkout for the next tier against the real API", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createCheckout).mockResolvedValue({ url: "#" });
    renderButtons("STARTER");
    fireEvent.click(screen.getByRole("button", { name: "Upgrade to Pro" }));
    await waitFor(() => expect(createCheckout).toHaveBeenCalledWith("PRO", "mock-token"));
  });

  it("offers subscribe (no portal) without a plan and hides upgrade on Territory", () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    const { unmount } = renderButtons(null);
    expect(screen.queryByRole("button", { name: "Manage billing" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Subscribe to Pro" })).toBeInTheDocument();
    unmount();
    renderButtons("TERRITORY");
    expect(screen.queryByRole("button", { name: /Upgrade/ })).not.toBeInTheDocument();
  });

  it("shows an error toast when the billing portal cannot be created", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createBillingPortal).mockRejectedValue(new Error("down"));
    renderButtons("PRO");
    fireEvent.click(screen.getByRole("button", { name: "Manage billing" }));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
  });
});
