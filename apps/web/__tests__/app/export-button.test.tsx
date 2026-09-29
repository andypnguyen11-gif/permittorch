// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()), exportLeadsCsv: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
vi.mock("firebase/auth", () => ({ signOut: vi.fn().mockResolvedValue(undefined) }));

import { ApiError, exportLeadsCsv } from "@/lib/api";
import { toast } from "sonner";
import { ExportButton } from "@/components/app/leads/export-button";

const csv = new Blob(["Score,Address\r\n90,1 Main St\r\n"], { type: "text/csv" });
const downloads: { name: string; href: string }[] = [];

beforeEach(() => {
  vi.clearAllMocks();
  downloads.length = 0;
  URL.createObjectURL = vi.fn(() => "blob:permittorch-test");
  URL.revokeObjectURL = vi.fn();
  vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
    downloads.push({ name: this.download, href: this.href });
  });
});

describe("ExportButton", () => {
  it("downloads the leads that match the current filters", async () => {
    vi.mocked(exportLeadsCsv).mockResolvedValue({ blob: csv, truncated: false });
    render(<ExportButton query={{ market: "mesa-az", minScore: 80, page: 3 }} />);

    fireEvent.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => expect(downloads).toEqual([
      { name: "permittorch-leads.csv", href: "blob:permittorch-test" },
    ]));
    expect(exportLeadsCsv).toHaveBeenCalledWith({ market: "mesa-az", minScore: 80, page: 3 }, "mock-token");
    expect(URL.createObjectURL).toHaveBeenCalledWith(csv);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:permittorch-test");
    expect(toast.success).toHaveBeenCalledWith("Export downloaded");
  });

  it("cannot be clicked twice while an export is running", async () => {
    let resolve!: (v: { blob: Blob; truncated: boolean }) => void;
    vi.mocked(exportLeadsCsv).mockReturnValue(new Promise((r) => { resolve = r; }));
    render(<ExportButton query={{}} />);

    fireEvent.click(screen.getByRole("button", { name: "Export CSV" }));

    expect(await screen.findByRole("button", { name: "Exporting…" })).toBeDisabled();
    resolve({ blob: csv, truncated: false });
    expect(await screen.findByRole("button", { name: "Export CSV" })).toBeEnabled();
  });

  it("says so when the export was cut at the limit", async () => {
    vi.mocked(exportLeadsCsv).mockResolvedValue({ blob: csv, truncated: true });
    render(<ExportButton query={{}} />);

    fireEvent.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => expect(toast).toHaveBeenCalledWith(
      "Export holds the first 5,000 leads. Narrow the filters to export the rest.",
    ));
    expect(downloads).toHaveLength(1);
    expect(toast.success).not.toHaveBeenCalled();
  });

  it("explains that the plan does not include export, and downloads nothing", async () => {
    vi.mocked(exportLeadsCsv).mockRejectedValue(
      new ApiError("CSV export requires the Pro or Territory plan", 403));
    render(<ExportButton query={{}} />);

    fireEvent.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(
      "CSV export is part of the Pro and Territory plans."));
    expect(downloads).toHaveLength(0);
  });

  it("reports any other failure", async () => {
    vi.mocked(exportLeadsCsv).mockRejectedValue(new Error("boom"));
    render(<ExportButton query={{}} />);

    fireEvent.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Could not export leads"));
    expect(downloads).toHaveLength(0);
  });
});
