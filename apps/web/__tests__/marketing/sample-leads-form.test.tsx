// @vitest-environment jsdom
//
// NOTE: the plan spec for this test used @testing-library/user-event, but
// that package is not among WS0's installed dependencies and WS3 may not
// edit package.json (file-ownership rule). Rewritten with fireEvent, which
// exercises the identical component behavior and assertions.
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { SampleLeadsForm } from "@/components/marketing/sample-leads-form";
import { mockMarkets } from "@/lib/fixtures/markets";
import * as api from "@/lib/api";

vi.mock("@/lib/api", () => ({ submitSampleLeadRequest: vi.fn() }));

// vitest.config.mts does not set `test.globals: true`, so
// @testing-library/react's afterEach-based auto-cleanup never registers.
// Clean up explicitly so each render() starts from an empty DOM.
afterEach(() => cleanup());

// NOTE: deliberately no beforeEach(() => mock.mockReset()) here — each test
// below sets its own mock implementation (mockResolvedValue/mockRejectedValue)
// before rendering, so a reset isn't needed for correctness. Calling
// mockReset()/mockClear() from inside a beforeEach hook in this vitest
// version misattributes the later test's own (properly caught) rejection as
// an unhandled one; see the plan/foundation adjustment note in the WS3 report.
describe("SampleLeadsForm", () => {
  function fillValid() {
    fireEvent.change(screen.getByLabelText(/name/i), { target: { value: "Dana Reyes" } });
    fireEvent.change(screen.getByLabelText(/work email/i), { target: { value: "dana@reyesfire.com" } });
    fireEvent.change(screen.getByLabelText(/company/i), { target: { value: "Reyes Fire Protection" } });
    fireEvent.change(screen.getByLabelText(/market/i), { target: { value: "houston-tx" } });
  }

  it("states exactly what a sample request delivers, with the opt-out", () => {
    render(<SampleLeadsForm markets={mockMarkets} />);
    const promise = screen.getByText(/up to 5 of the highest-scoring opportunities/);
    expect(promise.textContent).toContain("usually within the hour, then a weekly update — you can opt out from any email");
    expect(screen.queryByText(/5–10/)).toBeNull();
  });

  it("lists every market as an option", () => {
    render(<SampleLeadsForm markets={mockMarkets} />);
    for (const m of mockMarkets) {
      expect(screen.getByRole("option", { name: `${m.city}, ${m.state}` })).toBeDefined();
    }
  });

  it("shows validation errors and does not submit when empty", async () => {
    render(<SampleLeadsForm markets={mockMarkets} />);
    fireEvent.click(screen.getByRole("button", { name: /send my sample leads/i }));
    expect(await screen.findByText("Enter your name.")).toBeDefined();
    expect(screen.getByText("Enter a valid work email.")).toBeDefined();
    expect(screen.getByText("Enter your company name.")).toBeDefined();
    expect(screen.getByText("Pick a market.")).toBeDefined();
    expect(api.submitSampleLeadRequest).not.toHaveBeenCalled();
  });

  it("rejects a malformed email", async () => {
    render(<SampleLeadsForm markets={mockMarkets} />);
    fillValid();
    fireEvent.change(screen.getByLabelText(/work email/i), { target: { value: "not-an-email" } });
    fireEvent.click(screen.getByRole("button", { name: /send my sample leads/i }));
    expect(await screen.findByText("Enter a valid work email.")).toBeDefined();
    expect(api.submitSampleLeadRequest).not.toHaveBeenCalled();
  });

  it("submits the exact payload and shows the success state", async () => {
    vi.mocked(api.submitSampleLeadRequest).mockResolvedValue(undefined);
    render(<SampleLeadsForm markets={mockMarkets} />);
    fillValid();
    fireEvent.click(screen.getByRole("button", { name: /send my sample leads/i }));
    await waitFor(() =>
      expect(api.submitSampleLeadRequest).toHaveBeenCalledWith({
        name: "Dana Reyes",
        email: "dana@reyesfire.com",
        company: "Reyes Fire Protection",
        marketSlug: "houston-tx",
      }),
    );
    expect(await screen.findByText(/request received — check your inbox/i)).toBeDefined();
    expect(screen.getByText(/up to 5 of the highest-scoring opportunities \(score 70\+\)/)).toBeDefined();
    expect(screen.queryByText(/on the way/i)).toBeNull();
  });

  it("shows an error message when the request fails", async () => {
    vi.mocked(api.submitSampleLeadRequest).mockRejectedValue(new Error("boom"));
    render(<SampleLeadsForm markets={mockMarkets} />);
    fillValid();
    fireEvent.click(screen.getByRole("button", { name: /send my sample leads/i }));
    expect(await screen.findByText(/something went wrong/i)).toBeDefined();
  });
});
