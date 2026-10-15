// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()), updateEmailPreferences: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { updateEmailPreferences } from "@/lib/api";
import { toast } from "sonner";
import { DigestForm } from "@/components/app/alerts/digest-form";

beforeEach(() => vi.clearAllMocks());

describe("DigestForm", () => {
  it("preselects the current frequency", () => {
    render(<DigestForm initialFrequency="DAILY" />);
    expect(screen.getByRole("radio", { name: /Daily/ })).toBeChecked();
  });

  it("saves a new frequency and confirms with a toast", async () => {
    vi.mocked(updateEmailPreferences).mockResolvedValue(undefined);
    render(<DigestForm initialFrequency="DAILY" />);
    fireEvent.click(screen.getByRole("radio", { name: /Weekly/ }));
    expect(screen.getByRole("radio", { name: /Weekly/ })).toBeChecked();
    await waitFor(() => expect(updateEmailPreferences).toHaveBeenCalledWith("WEEKLY", "mock-token"));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Digest set to weekly"));
  });

  it("marks itself busy and ignores input while saving, without disabling the options", async () => {
    let resolve!: () => void;
    vi.mocked(updateEmailPreferences).mockReturnValue(new Promise<void>((r) => { resolve = r; }));
    render(<DigestForm initialFrequency="DAILY" />);
    fireEvent.click(screen.getByRole("radio", { name: /Weekly/ }));
    const group = screen.getByRole("group", { name: "Digest frequency" });
    expect(group).toHaveAttribute("aria-busy", "true");
    expect(group).not.toBeDisabled();
    expect(screen.getByRole("radio", { name: /Off/ })).toBeEnabled();
    fireEvent.click(screen.getByRole("radio", { name: /Off/ }));
    expect(screen.getByRole("radio", { name: /Weekly/ })).toBeChecked();
    await waitFor(() => expect(updateEmailPreferences).toHaveBeenCalledTimes(1));
    expect(updateEmailPreferences).toHaveBeenCalledWith("WEEKLY", "mock-token");
    resolve();
    await waitFor(() => expect(group).toHaveAttribute("aria-busy", "false"));
  });

  it("reverts the selection when saving fails", async () => {
    vi.mocked(updateEmailPreferences).mockRejectedValue(new Error("boom"));
    render(<DigestForm initialFrequency="DAILY" />);
    fireEvent.click(screen.getByRole("radio", { name: /Off/ }));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.getByRole("radio", { name: /Daily/ })).toBeChecked();
  });
});

describe("DigestForm market picker", () => {
  const MARKETS = [
    { id: "h", slug: "houston-tx", name: "Houston", city: "Houston", state: "TX" },
    { id: "d", slug: "dallas-tx", name: "Dallas", city: "Dallas", state: "TX" },
  ];

  it("checks the saved markets and saves a toggled market with the frequency", async () => {
    vi.mocked(updateEmailPreferences).mockResolvedValue(undefined);
    render(<DigestForm initialFrequency="DAILY" markets={MARKETS} initialMarketSlugs={["houston-tx"]} />);
    expect(screen.getByRole("checkbox", { name: "Houston" })).toBeChecked();
    fireEvent.click(screen.getByRole("checkbox", { name: "Dallas" }));
    expect(screen.getByRole("checkbox", { name: "Dallas" })).toBeChecked();
    await waitFor(() => expect(updateEmailPreferences)
      .toHaveBeenCalledWith("DAILY", "mock-token", ["houston-tx", "dallas-tx"]));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Digest markets saved"));
  });

  it("sends the picked markets when the frequency changes", async () => {
    vi.mocked(updateEmailPreferences).mockResolvedValue(undefined);
    render(<DigestForm initialFrequency="NONE" markets={MARKETS} initialMarketSlugs={["dallas-tx"]} />);
    fireEvent.click(screen.getByRole("radio", { name: /Weekly/ }));
    await waitFor(() => expect(updateEmailPreferences).toHaveBeenCalledWith("WEEKLY", "mock-token", ["dallas-tx"]));
  });

  it("unchecks a market again when saving fails", async () => {
    vi.mocked(updateEmailPreferences).mockRejectedValue(new Error("boom"));
    render(<DigestForm initialFrequency="DAILY" markets={MARKETS} initialMarketSlugs={[]} />);
    fireEvent.click(screen.getByRole("checkbox", { name: "Houston" }));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.getByRole("checkbox", { name: "Houston" })).not.toBeChecked();
  });

  it("during the free pilot, says nothing is sent until a market is picked", () => {
    const { rerender } = render(
      <DigestForm initialFrequency="DAILY" markets={MARKETS} initialMarketSlugs={[]} freeAccess />);
    expect(screen.getByText("No email is sent until you pick at least one market.")).toBeInTheDocument();
    rerender(<DigestForm key="picked" initialFrequency="DAILY" markets={MARKETS} initialMarketSlugs={["houston-tx"]} freeAccess />);
    expect(screen.queryByText(/No email is sent/)).not.toBeInTheDocument();
  });

  it("shows no note while the digest is off", () => {
    render(<DigestForm initialFrequency="NONE" markets={MARKETS} initialMarketSlugs={[]} freeAccess />);
    expect(screen.queryByText(/No email is sent/)).not.toBeInTheDocument();
  });

  it("on a paid plan, says an empty pick covers every market on the plan", () => {
    render(<DigestForm initialFrequency="WEEKLY" markets={MARKETS} initialMarketSlugs={[]} />);
    expect(screen.getByText("No market picked, so the digest covers every market on your plan.")).toBeInTheDocument();
  });

  it("hides the picker when there are no markets to pick", () => {
    render(<DigestForm initialFrequency="DAILY" markets={[]} />);
    expect(screen.queryByRole("heading", { name: "Markets in your digest" })).not.toBeInTheDocument();
  });
});
