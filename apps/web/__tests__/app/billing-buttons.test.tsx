// @vitest-environment jsdom
import "./dom-cleanup";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()), createBillingPortal: vi.fn(), createCheckout: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { createBillingPortal, createCheckout } from "@/lib/api";
import { toast } from "sonner";
import { BillingButtons, CheckoutPicker, nextPlan } from "@/components/app/account/billing-buttons";
import type { Market } from "@permittorch/types";
import { TooltipProvider } from "@/components/ui/tooltip";

const renderButtons = (plan: Parameters<typeof BillingButtons>[0]["plan"]) =>
  render(<TooltipProvider><BillingButtons plan={plan} /></TooltipProvider>);

const m = (slug: string, name: string): Market => ({ id: slug, slug, name, city: name, state: "TX" });
const MARKETS = ["Austin", "Dallas", "Houston", "El Paso", "Fort Worth", "San Antonio"]
  .map((name) => m(`${name.toLowerCase().replace(" ", "-")}-tx`, name));
const renderPicker = (initialPlan: Parameters<typeof CheckoutPicker>[0]["initialPlan"] = "PRO") =>
  render(<TooltipProvider><CheckoutPicker markets={MARKETS} initialPlan={initialPlan} /></TooltipProvider>);

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

  it("sends plan holders' upgrades to the billing portal (a second checkout is a 409)", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createBillingPortal).mockResolvedValue({ url: "#" });
    renderButtons("STARTER");
    fireEvent.click(screen.getByRole("button", { name: "Upgrade to Pro" }));
    await waitFor(() => expect(createBillingPortal).toHaveBeenCalledWith("mock-token"));
    expect(createCheckout).not.toHaveBeenCalled();
  });

  it("hides upgrade on Territory", () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    renderButtons("TERRITORY");
    expect(screen.getByRole("button", { name: "Manage billing" })).toBeInTheDocument();
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

describe("CheckoutPicker", () => {
  it("preselects the requested plan and requires a market before checkout", () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    renderPicker("STARTER");
    expect(screen.getByRole("radio", { name: "Starter" })).toBeChecked();
    expect(screen.getByRole("button", { name: "Subscribe to Starter" })).toBeDisabled();
  });

  it("Starter/Pro: exactly one market, posted with the plan", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createCheckout).mockResolvedValue({ url: "#" });
    renderPicker("PRO");
    fireEvent.click(screen.getByRole("radio", { name: "Austin" }));
    fireEvent.click(screen.getByRole("radio", { name: "Dallas" }));
    expect(screen.getByRole("radio", { name: "Austin" })).not.toBeChecked();
    fireEvent.click(screen.getByRole("button", { name: "Subscribe to Pro" }));
    await waitFor(() => expect(createCheckout).toHaveBeenCalledWith("PRO", ["dallas-tx"], "mock-token"));
  });

  it("Territory: up to five markets, the sixth is disabled", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createCheckout).mockResolvedValue({ url: "#" });
    renderPicker("TERRITORY");
    for (const name of ["Austin", "Dallas", "Houston", "El Paso", "Fort Worth"])
      fireEvent.click(screen.getByRole("checkbox", { name }));
    expect(screen.getByRole("checkbox", { name: "San Antonio" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Subscribe to Territory" }));
    await waitFor(() => expect(createCheckout).toHaveBeenCalledWith(
      "TERRITORY", ["austin-tx", "dallas-tx", "houston-tx", "el-paso-tx", "fort-worth-tx"], "mock-token"));
  });

  it("trims a Territory selection to one market when switching to Pro", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.mocked(createCheckout).mockResolvedValue({ url: "#" });
    renderPicker("TERRITORY");
    fireEvent.click(screen.getByRole("checkbox", { name: "Houston" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Austin" }));
    fireEvent.click(screen.getByRole("radio", { name: "Pro" }));
    fireEvent.click(screen.getByRole("button", { name: "Subscribe to Pro" }));
    await waitFor(() => expect(createCheckout).toHaveBeenCalledWith("PRO", ["houston-tx"], "mock-token"));
  });

  it("is disabled in mock mode", () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1");
    renderPicker("PRO");
    fireEvent.click(screen.getByRole("radio", { name: "Austin" }));
    expect(screen.getByRole("button", { name: "Subscribe to Pro" })).toBeDisabled();
  });
});
