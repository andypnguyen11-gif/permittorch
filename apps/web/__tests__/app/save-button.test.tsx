// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()), saveLead: vi.fn(), unsaveLead: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { saveLead, unsaveLead } from "@/lib/api";
import { toast } from "sonner";
import { SaveButton } from "@/components/app/lead-detail/save-button";

beforeEach(() => vi.clearAllMocks());

describe("SaveButton", () => {
  it("saves optimistically and keeps the saved id from the API", async () => {
    let resolve!: (v: unknown) => void;
    vi.mocked(saveLead).mockReturnValue(new Promise((r) => { resolve = r; }) as never);
    render(<SaveButton leadId="lead-001" savedId={null} />);
    fireEvent.click(screen.getByRole("button", { name: "Save lead" }));
    expect(await screen.findByRole("button", { name: "Saved" })).toBeInTheDocument();
    expect(saveLead).toHaveBeenCalledWith("lead-001", "mock-token");
    resolve({ id: "saved-9" });
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Lead saved"));

    vi.mocked(unsaveLead).mockResolvedValue(undefined);
    fireEvent.click(screen.getByRole("button", { name: "Saved" }));
    await waitFor(() => expect(unsaveLead).toHaveBeenCalledWith("saved-9", "mock-token"));
    expect(await screen.findByRole("button", { name: "Save lead" })).toBeInTheDocument();
  });

  it("reverts and shows an error toast when saving fails", async () => {
    vi.mocked(saveLead).mockRejectedValue(new Error("boom"));
    render(<SaveButton leadId="lead-001" savedId={null} />);
    fireEvent.click(screen.getByRole("button", { name: "Save lead" }));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.getByRole("button", { name: "Save lead" })).toBeInTheDocument();
  });
});
