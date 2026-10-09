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
    // The link opens this record, not the portal's home page.
    expect(source).toHaveAttribute("href", "https://www.houstonpermittingcenter.org/permits/25-176389");
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

  it("shows the humanized new permit fields when present, hides them when null, and has no Record type field", async () => {
    // lead-011: fire contractor already assigned; has workType/propertyType/expirationDate,
    // but no recordType/inspectionDate/businessName. It's a permit record, so the
    // always-shown fields (Filed, Issued, Owner, Contractor, etc.) still render.
    await renderPage("lead-011");
    const permit = screen.getByRole("region", { name: "Permit" });
    expect(within(permit).getByText("Corrective repair")).toBeInTheDocument();
    expect(within(permit).getByText("Parking structure")).toBeInTheDocument();
    expect(within(permit).queryByText("Record type")).not.toBeInTheDocument();
    expect(within(permit).queryByText("Inspection date")).not.toBeInTheDocument();
    expect(within(permit).queryByText("Business")).not.toBeInTheDocument();
    expect(within(permit).getByText("Owner")).toBeInTheDocument();
    expect(within(permit).getByText("Contractor")).toBeInTheDocument();
    // The system-type field is humanized from the machine permitType.
    expect(within(permit).getByText("System type")).toBeInTheDocument();
    expect(within(permit).getByText("Standpipe repair")).toBeInTheDocument();
    // The fire-contractor-assigned signal is visible in the score breakdown.
    expect(screen.getByText("A fire-protection contractor is already on this permit")).toBeInTheDocument();
  });

  it("titles the card 'Inspection' with a 'Record number' label, no 'Record type' field, and hides never-applicable fields", async () => {
    // lead-013: recordType inspection, rawStatus differs from the "Failed" label,
    // estimatedValue/issuedDate/contractorName are all null (never applicable to
    // an inspection) and must be hidden entirely rather than shown with a dash.
    await renderPage("lead-013");
    const record = screen.getByRole("region", { name: "Inspection" });
    expect(within(record).getByText("Record number")).toBeInTheDocument();
    expect(within(record).queryByText("Permit number")).not.toBeInTheDocument();
    expect(within(record).queryByText("Record type")).not.toBeInTheDocument();
    // Its system type is "inspection", which the card title already says.
    expect(within(record).queryByText("System type")).not.toBeInTheDocument();
    expect(within(record).getByText("Business")).toBeInTheDocument();
    expect(within(record).getByText("Inspection date")).toBeInTheDocument();
    // Present fields still show (Filed, Square footage, Owner).
    expect(within(record).getByText("Filed")).toBeInTheDocument();
    expect(within(record).getByText("Owner")).toBeInTheDocument();
    // Never-applicable, empty fields for this record are hidden, not dashed.
    expect(within(record).queryByText("Issued")).not.toBeInTheDocument();
    expect(within(record).queryByText("Estimated value")).not.toBeInTheDocument();
    expect(within(record).queryByText("Contractor")).not.toBeInTheDocument();
    expect(screen.getByText("Failed · Open/Follow-Up Needed")).toBeInTheDocument();
    expect(screen.getByText("Status")).toBeInTheDocument();
  });

  it("calls a link to the raw data row source data, not the original record", async () => {
    await renderPage("lead-013");
    const source = screen.getByRole("link", { name: /View source data for this record/ });
    expect(source).toHaveAttribute("href",
      "https://data.houstontx.gov/resource/fire-inspections.json?record_id=25-175772");
    expect(source).toHaveAttribute("target", "_blank");
    expect(source).toHaveAttribute("rel", "noopener noreferrer");
    expect(screen.queryByRole("link", { name: /View original record/ })).not.toBeInTheDocument();
  });

  it("links to the dataset, and says so, when the record has no link of its own", async () => {
    await renderPage("lead-022");
    const source = screen.getByRole("link", { name: /View source dataset/ });
    expect(source).toHaveAttribute("href",
      "https://dallascityhall.com/departments/sustainabledevelopment/");
    expect(screen.queryByRole("link", { name: /View original record/ })).not.toBeInTheDocument();
  });

  it("shows no link at all when the source has no web address", async () => {
    const lead = await api.getLead("lead-022", "mock-token");
    const spy = vi.spyOn(api, "getLead").mockResolvedValueOnce({
      ...lead, source: { ...lead.source, url: "", recordUrl: null, recordUrlKind: null },
    });
    await renderPage("lead-022");
    const region = screen.getByRole("region", { name: "Source" });
    expect(within(region).queryByRole("link")).not.toBeInTheDocument();
    expect(within(region).queryByText(/^View /)).not.toBeInTheDocument();
    expect(within(region).getByText("City of Dallas Permits")).toBeInTheDocument();
    spy.mockRestore();
  });

  it("a New Jersey lead says the source does not publish the contractor and dates its data", async () => {
    const lead = await api.getLead("lead-022", "mock-token");
    const spy = vi.spyOn(api, "getLead").mockResolvedValueOnce({
      ...lead, contractorStatus: "NOT_PUBLISHED",
      reason: "This source does not publish the contractor. The record mentions fire subcode work.",
      signals: lead.signals.filter((sig) => sig.signalType !== "NO_CONTRACTOR_LISTED"),
      permit: { ...lead.permit, contractorName: null, ownerName: null, zip: null },
      source: { ...lead.source, cadence: "MONTHLY", dataThrough: "2026-08-07T00:00:00Z" },
    });
    await renderPage("lead-022");
    const permit = screen.getByRole("region", { name: "Permit" });
    expect(within(permit).getByText("Not published by this source")).toBeInTheDocument();
    expect(screen.queryByText(/No contractor listed/)).not.toBeInTheDocument();
    const source = screen.getByRole("region", { name: "Source" });
    expect(within(source).getByText("Data through Aug 7, 2026, published monthly")).toBeInTheDocument();
    spy.mockRestore();
  });

  it("a daily source shows no data date", async () => {
    await renderPage("lead-022");
    const source = screen.getByRole("region", { name: "Source" });
    expect(within(source).queryByText(/published monthly/)).not.toBeInTheDocument();
  });

  it("renders a CONTRACTOR participant with the label 'Contractor'", async () => {
    await renderPage("lead-014");
    const participants = screen.getByRole("region", { name: "Participants" });
    expect(within(participants).getByText("Contractor")).toBeInTheDocument();
    expect(within(participants).getByText("Bayou Sprinkler Co.")).toBeInTheDocument();
  });

  it("shows a participant's phone, email and licence number from the permit record", async () => {
    await renderPage("lead-014");
    const participants = screen.getByRole("region", { name: "Participants" });
    const phone = within(participants).getByRole("link", { name: "(713) 555-0142" });
    expect(phone).toHaveAttribute("href", "tel:+17135550142");
    const email = within(participants).getByRole("link", { name: "office@example.com" });
    expect(email).toHaveAttribute("href", "mailto:office@example.com");
    expect(within(participants).getByText("License 000000")).toBeInTheDocument();
    // Says where the details come from: the record, not a lookup.
    expect(within(participants).getByText("Contact details are shown as the public permit record lists them."))
      .toBeInTheDocument();
    // The owner has none, and shows none.
    expect(within(participants).getAllByRole("link")).toHaveLength(2);
  });

  it("shows a phone it cannot safely dial as plain text", async () => {
    await renderPage("lead-011");
    const participants = screen.getByRole("region", { name: "Participants" });
    expect(within(participants).getByText("713-555-0177 x12")).toBeInTheDocument();
    expect(within(participants).queryByRole("link")).not.toBeInTheDocument();
  });

  it("says nothing about contact details when the record publishes none", async () => {
    await renderPage("lead-001");
    const participants = screen.getByRole("region", { name: "Participants" });
    expect(within(participants).getByText("Katy Freeway Industrial LP")).toBeInTheDocument();
    expect(within(participants).queryByRole("link")).not.toBeInTheDocument();
    expect(within(participants).queryByText(/Contact details are shown/)).not.toBeInTheDocument();
    expect(within(participants).queryByText(/^License /)).not.toBeInTheDocument();
  });

  it("never turns a contact value from the API into a link it did not check", async () => {
    const lead = await api.getLead("lead-014", "mock-token");
    const spy = vi.spyOn(api, "getLead").mockResolvedValueOnce({
      ...lead,
      participants: [{
        role: "CONTRACTOR", name: "Bayou Sprinkler Co.", phone: "javascript:alert(1)",
        email: "office@example.com?bcc=other@example.com", licenseNumber: null,
      }],
    });
    await renderPage("lead-014");
    const participants = screen.getByRole("region", { name: "Participants" });
    expect(within(participants).queryByRole("link")).not.toBeInTheDocument();
    spy.mockRestore();
  });

  it("still renders participants from an API response that has no contact fields", async () => {
    const lead = await api.getLead("lead-014", "mock-token");
    const spy = vi.spyOn(api, "getLead").mockResolvedValueOnce({
      ...lead,
      participants: [{ role: "CONTRACTOR", name: "Bayou Sprinkler Co." }] as never,
    });
    await renderPage("lead-014");
    const participants = screen.getByRole("region", { name: "Participants" });
    expect(within(participants).getByText("Bayou Sprinkler Co.")).toBeInTheDocument();
    expect(within(participants).queryByRole("link")).not.toBeInTheDocument();
    spy.mockRestore();
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

describe("/app/leads/[id] contractor status", () => {
  it("badges the header when the permit names a fire contractor", async () => {
    // lead-011 carries FIRE_CONTRACTOR_NAMED in the fixtures.
    await renderPage("lead-011");
    const header = screen.getByRole("heading", { level: 1 }).closest("div")!;
    expect(within(header).getByText("Fire contractor on permit")).toBeInTheDocument();
  });

  it("shows no contractor badge when no fire contractor is on the permit", async () => {
    // lead-001 has NO_CONTRACTOR_LISTED.
    await renderPage("lead-001");
    expect(screen.queryByText("Fire contractor on permit")).not.toBeInTheDocument();
  });
});
