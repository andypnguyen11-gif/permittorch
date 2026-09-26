// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import "./dom-cleanup";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { SavedLeadItem } from "@permittorch/types";

vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()),
  updateSavedLead: vi.fn(),
  unsaveLead: vi.fn(),
}));
vi.mock("@/components/app/use-api-token", () => ({
  useApiToken: () => async () => "mock-token",
}));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { unsaveLead, updateSavedLead } from "@/lib/api";
import { SavedList } from "@/components/app/saved/saved-list";

const item: SavedLeadItem = {
  id: "saved-001", status: "SAVED", createdAt: new Date().toISOString(),
  lead: {
    id: "lead-001", score: 94, title: "Warehouse Fire Sprinkler System",
    address: "8811 Katy Fwy", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: new Date().toISOString(), estimatedValue: 1850000,
    reason: "New commercial warehouse with full sprinkler scope.", isNew: true,
  },
};

beforeEach(() => vi.clearAllMocks());

describe("SavedList", () => {
  it("toggles status to Contacted optimistically before the API resolves", async () => {
    let resolveApi!: () => void;
    vi.mocked(updateSavedLead).mockReturnValue(new Promise((r) => { resolveApi = () => r(); }));
    render(<SavedList initialItems={[item]} />);

    fireEvent.click(screen.getByRole("button", { name: "Mark contacted" }));
    // Optimistic: UI flips while the promise is still pending.
    expect(screen.getByRole("button", { name: "Mark saved" })).toBeInTheDocument();
    await waitFor(() =>
      expect(updateSavedLead).toHaveBeenCalledWith("saved-001", "CONTACTED", "mock-token"),
    );
    resolveApi();
  });

  it("ignores rapid repeat toggles on an item until its request settles", async () => {
    let resolveApi!: () => void;
    vi.mocked(updateSavedLead).mockReturnValue(new Promise((r) => { resolveApi = () => r(); }));
    const other: SavedLeadItem = { ...item, id: "saved-002",
      lead: { ...item.lead, id: "lead-007", title: "Restaurant Kitchen Hood Suppression" } };
    render(<SavedList initialItems={[item, other]} />);

    const [first] = screen.getAllByRole("button", { name: "Mark contacted" });
    fireEvent.click(first);
    const flipped = screen.getByRole("button", { name: "Mark saved" });
    fireEvent.click(flipped);
    fireEvent.click(flipped);
    expect(flipped).toBeDisabled();
    // Other items stay interactive.
    expect(screen.getByRole("button", { name: "Mark contacted" })).toBeEnabled();
    await waitFor(() => expect(updateSavedLead).toHaveBeenCalledTimes(1));
    expect(updateSavedLead).toHaveBeenCalledWith("saved-001", "CONTACTED", "mock-token");

    resolveApi();
    await waitFor(() => expect(screen.getByRole("button", { name: "Mark saved" })).toBeEnabled());
    vi.mocked(updateSavedLead).mockResolvedValue(undefined);
    fireEvent.click(screen.getByRole("button", { name: "Mark saved" }));
    await waitFor(() => expect(updateSavedLead).toHaveBeenCalledTimes(2));
    expect(updateSavedLead).toHaveBeenLastCalledWith("saved-001", "SAVED", "mock-token");
  });

  it("reverts the status when the API call fails", async () => {
    vi.mocked(updateSavedLead).mockRejectedValue(new Error("boom"));
    render(<SavedList initialItems={[item]} />);
    fireEvent.click(screen.getByRole("button", { name: "Mark contacted" }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Mark contacted" })).toBeInTheDocument(),
    );
  });

  it("removes an item optimistically and shows the empty state", async () => {
    vi.mocked(unsaveLead).mockResolvedValue(undefined);
    render(<SavedList initialItems={[item]} />);
    fireEvent.click(screen.getByRole("button", { name: "Remove" }));
    await waitFor(() => expect(unsaveLead).toHaveBeenCalledWith("saved-001", "mock-token"));
    await waitFor(() =>
      expect(screen.getByText("No saved leads yet")).toBeInTheDocument(),
    );
  });

  it("renders the empty state for no items", () => {
    render(<SavedList initialItems={[]} />);
    expect(screen.getByText("No saved leads yet")).toBeInTheDocument();
  });

  it("filters by status with live counts", () => {
    const contacted: SavedLeadItem = { ...item, id: "saved-002", status: "CONTACTED",
      lead: { ...item.lead, id: "lead-007", title: "Restaurant Kitchen Hood Suppression" } };
    render(<SavedList initialItems={[item, contacted]} />);
    expect(screen.getByRole("button", { name: "All 2" })).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(screen.getByRole("button", { name: "Contacted 1" }));
    expect(screen.queryByText("Warehouse Fire Sprinkler System")).not.toBeInTheDocument();
    expect(screen.getByText("Restaurant Kitchen Hood Suppression")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "To contact 1" }));
    expect(screen.getByText("Warehouse Fire Sprinkler System")).toBeInTheDocument();
  });
});
