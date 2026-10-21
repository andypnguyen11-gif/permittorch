// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import "./dom-cleanup";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { LeadSummary, SavedLeadItem } from "@permittorch/types";

vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()),
  setLeadOutcome: vi.fn(),
  clearLeadOutcome: vi.fn(),
}));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("@/components/app/sign-out", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/components/app/sign-out")>()),
  reportMutationError: vi.fn(),
}));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { clearLeadOutcome, setLeadOutcome } from "@/lib/api";
import { reportMutationError } from "@/components/app/sign-out";
import { OutcomeControl } from "@/components/app/lead-outcome/outcome-control";
import { OUTCOME_LABELS } from "@/components/app/lead-outcome/outcome-labels";
import { LeadTable } from "@/components/app/leads/lead-table";
import { SavedList } from "@/components/app/saved/saved-list";

const trigger = () => screen.getByRole("button", { name: "Lead outcome" });

// Base UI commits a menu item on the full pointer sequence, as in top-bar.test.
async function choose(label: string) {
  fireEvent.click(trigger());
  const item = await screen.findByRole("menuitem", { name: label });
  fireEvent.pointerMove(item);
  fireEvent.mouseMove(item);
  fireEvent.pointerDown(item);
  fireEvent.mouseDown(item);
  fireEvent.pointerUp(item);
  fireEvent.mouseUp(item);
  fireEvent.click(item);
}

const deferred = () => {
  let resolve!: () => void; let reject!: (e: unknown) => void;
  const promise = new Promise<void>((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
};

const lead = (o: Partial<LeadSummary> = {}): LeadSummary => ({
  id: "lead-1", score: 88, title: "Warehouse sprinkler", address: "1 Main St", city: "Houston", state: "TX",
  category: "FIRE_SPRINKLER", permitType: null, status: "NEW", filedDate: null, estimatedValue: null,
  reason: "r", isNew: false, contractorStatus: null, firstSeenAt: null, myOutcome: null, ...o,
});

beforeEach(() => {
  vi.mocked(setLeadOutcome).mockReset().mockResolvedValue(undefined);
  vi.mocked(clearLeadOutcome).mockReset().mockResolvedValue(undefined);
  vi.mocked(reportMutationError).mockReset();
});

describe("OutcomeControl", () => {
  it("labels the four outcomes in the contractor's words", () => {
    expect(OUTCOME_LABELS).toEqual({
      WORTH_CONTACTING: "Worth contacting", ALREADY_AWARDED: "Already awarded",
      WRONG_TRADE: "Wrong trade", TOO_LATE: "Too late",
    });
  });

  it("offers 'Tag' when untagged and sets the chosen outcome", async () => {
    render(<OutcomeControl leadId="lead-1" outcome={null} />);
    expect(trigger()).toHaveTextContent("Tag");
    await choose("Wrong trade");
    await waitFor(() => expect(setLeadOutcome).toHaveBeenCalledWith("lead-1", "WRONG_TRADE", "mock-token"));
    await waitFor(() => expect(trigger()).toHaveTextContent("Wrong trade"));
  });

  it("clears the tag when the current outcome is chosen again", async () => {
    render(<OutcomeControl leadId="lead-1" outcome="TOO_LATE" />);
    expect(trigger()).toHaveTextContent("Too late");
    await choose("Too late");
    await waitFor(() => expect(clearLeadOutcome).toHaveBeenCalledWith("lead-1", "mock-token"));
    await waitFor(() => expect(trigger()).toHaveTextContent("Tag"));
  });

  it("clears the tag from 'Clear'", async () => {
    render(<OutcomeControl leadId="lead-1" outcome="WORTH_CONTACTING" />);
    await choose("Clear");
    await waitFor(() => expect(clearLeadOutcome).toHaveBeenCalledWith("lead-1", "mock-token"));
  });

  it("offers no 'Clear' when untagged", async () => {
    render(<OutcomeControl leadId="lead-1" outcome={null} />);
    fireEvent.click(trigger());
    await screen.findByRole("menuitem", { name: "Worth contacting" });
    expect(screen.queryByRole("menuitem", { name: "Clear" })).not.toBeInTheDocument();
  });

  it("reverts and reports when the call fails", async () => {
    vi.mocked(setLeadOutcome).mockRejectedValueOnce(new Error("boom"));
    render(<OutcomeControl leadId="lead-1" outcome="TOO_LATE" />);
    await choose("Worth contacting");
    await waitFor(() => expect(reportMutationError).toHaveBeenCalledWith(expect.any(Error), "Could not update the lead tag"));
    expect(trigger()).toHaveTextContent("Too late");
  });

  it("sends rapid choices in order so the last one wins", async () => {
    const first = deferred();
    vi.mocked(setLeadOutcome).mockReturnValueOnce(first.promise).mockResolvedValueOnce(undefined);
    render(<OutcomeControl leadId="lead-1" outcome={null} />);
    await choose("Too late");
    await choose("Worth contacting");
    expect(trigger()).toHaveTextContent("Worth contacting");
    expect(setLeadOutcome).toHaveBeenCalledTimes(1);   // the second waits for the first
    first.resolve();
    await waitFor(() => expect(setLeadOutcome).toHaveBeenCalledTimes(2));
    expect(vi.mocked(setLeadOutcome).mock.calls.map((c) => c[1])).toEqual(["TOO_LATE", "WORTH_CONTACTING"]);
    expect(trigger()).toHaveTextContent("Worth contacting");
  });

  it("keeps a later choice when an earlier one fails", async () => {
    const first = deferred();
    vi.mocked(setLeadOutcome).mockReturnValueOnce(first.promise).mockResolvedValueOnce(undefined);
    render(<OutcomeControl leadId="lead-1" outcome={null} />);
    await choose("Too late");
    await choose("Wrong trade");
    first.reject(new Error("boom"));
    await waitFor(() => expect(setLeadOutcome).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(trigger()).toHaveTextContent("Wrong trade"));
  });
});

describe("OutcomeControl placement", () => {
  it.each([false, true])("sits above the row's stretched link in the lead table (compact %s)", async (compact) => {
    render(<LeadTable compact={compact} leads={[lead({ myOutcome: "ALREADY_AWARDED" })]} />);
    const link = screen.getByRole("link", { name: "Warehouse sprinkler" });
    const navigate = vi.fn((e: Event) => e.preventDefault());
    link.addEventListener("click", navigate);
    expect(trigger()).toHaveTextContent("Already awarded");
    expect(link.contains(trigger())).toBe(false);
    expect(trigger().closest(".relative.z-\\[1\\]")).not.toBeNull();
    await choose("Too late");
    await waitFor(() => expect(setLeadOutcome).toHaveBeenCalled());
    expect(navigate).not.toHaveBeenCalled();
  });

  it("shows the label and menu on the saved list", () => {
    const item: SavedLeadItem = { id: "s1", status: "SAVED", createdAt: new Date().toISOString(),
      lead: lead({ myOutcome: "WRONG_TRADE" }) };
    render(<SavedList initialItems={[item]} />);
    const row = screen.getByRole("listitem");
    expect(within(row).getByRole("button", { name: "Lead outcome" })).toHaveTextContent("Wrong trade");
  });
});
