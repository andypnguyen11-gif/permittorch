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
import { SESSION_EXPIRED_DIGEST } from "@/components/app/session-digest";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));

const renderPage = async (id: string) =>
  render(await LeadDetailPage({ params: Promise.resolve({ id }) }));

describe("/app/leads/[id] page (mock API)", () => {
  it("renders the curated lead with its full score explanation and source link", async () => {
    await renderPage("lead-001");
    expect(screen.getByRole("heading", { level: 1, name: "Warehouse Fire Sprinkler System" })).toBeInTheDocument();
    expect(screen.getByText("Why this is a 100")).toBeInTheDocument();
    // BASE_SCORE + six rule signals; the sum (125) is shown as capped.
    expect(screen.getAllByTestId("signal-weight")).toHaveLength(7);
    expect(screen.getByTestId("signal-total")).toHaveTextContent("100");
    expect(screen.getByTestId("signal-clamp-note")).toHaveTextContent("capped at 100");
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

  it("shows the humanized new permit fields when present, and hides them when null", async () => {
    // lead-011: fire contractor already assigned; has workType/propertyType/expirationDate,
    // but no recordType/inspectionDate/businessName.
    await renderPage("lead-011");
    const permit = screen.getByRole("region", { name: "Permit" });
    expect(within(permit).getByText("Standpipe repair")).toBeInTheDocument();
    expect(within(permit).getByText("Parking structure")).toBeInTheDocument();
    expect(within(permit).queryByText("Record type")).not.toBeInTheDocument();
    expect(within(permit).queryByText("Inspection date")).not.toBeInTheDocument();
    expect(within(permit).queryByText("Business")).not.toBeInTheDocument();
    // The fire-contractor-assigned signal is visible in the score breakdown.
    expect(screen.getByText("A fire-protection contractor is already on this permit")).toBeInTheDocument();
  });

  it("titles the card 'Inspection' with a 'Record number' label, and shows raw status and inspection date", async () => {
    // lead-013: recordType inspection, rawStatus differs from the FAILED label.
    await renderPage("lead-013");
    const record = screen.getByRole("region", { name: "Inspection" });
    expect(within(record).getByText("Record number")).toBeInTheDocument();
    expect(within(record).queryByText("Permit number")).not.toBeInTheDocument();
    expect(within(record).getByText("Record type")).toBeInTheDocument(); // humanized recordType field label
    expect(within(record).getAllByText("Inspection").length).toBeGreaterThanOrEqual(2); // card title + field value
    expect(within(record).getByText("Business")).toBeInTheDocument();
    expect(within(record).getByText("Inspection date")).toBeInTheDocument();
    expect(screen.getByText("Failed · Open/Follow-Up Needed")).toBeInTheDocument();
  });

  it("renders a CONTRACTOR participant with the label 'Contractor'", async () => {
    await renderPage("lead-014");
    const participants = screen.getByRole("region", { name: "Participants" });
    expect(within(participants).getByText("Contractor")).toBeInTheDocument();
    expect(within(participants).getByText("Bayou Sprinkler Co.")).toBeInTheDocument();
  });

  it("renders not-found for an unknown lead id in mock mode", async () => {
    await expect(renderPage("lead-does-not-exist")).rejects.toThrow("NOT_FOUND");
  });

  it("maps a 404 from the API to the not-found page", async () => {
    const spy = vi.spyOn(api, "getLead").mockRejectedValueOnce(new api.ApiError("nope", 404));
    await expect(renderPage("lead-404")).rejects.toThrow("NOT_FOUND");
    spy.mockRestore();
  });

  it("tags a 401 for the session-recovery boundary instead of redirecting to /login", async () => {
    const spy = vi.spyOn(api, "getLead").mockRejectedValueOnce(new api.ApiError("expired", 401));
    await expect(renderPage("lead-001")).rejects.toMatchObject({
      status: 401,
      digest: SESSION_EXPIRED_DIGEST,
    });
    spy.mockRestore();
  });
});
