// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";

const refresh = vi.fn();
vi.mock("next/navigation", () => ({
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
  useRouter: () => ({ refresh }),
  usePathname: () => "/app/admin/removals",
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import * as api from "@/lib/api";
import { toast } from "sonner";
import AdminRemovalsPage from "@/app/app/admin/removals/page";
import { RemovalForm } from "@/components/app/admin/removal-form";
import { RemovalTable } from "@/components/app/admin/removal-table";
import { Sidebar } from "@/components/app/sidebar";
import { mockAccountMe } from "@/lib/fixtures/account";
import { mockRemovals } from "@/lib/fixtures/admin";
import { mockMarkets } from "@/lib/fixtures/markets";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));
beforeEach(() => { vi.clearAllMocks(); vi.restoreAllMocks(); });

const markets = mockMarkets.slice(0, 2);
const fill = (label: string | RegExp, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
const choose = (kind: string) => fireEvent.change(screen.getByLabelText("What to remove"), { target: { value: kind } });
const removeButton = () => screen.getByRole("button", { name: "Remove" });

describe("the removals page", () => {
  it("shows the form and the list to a super admin", async () => {
    render(await AdminRemovalsPage());
    expect(screen.getByRole("heading", { level: 1, name: "Removals" })).toBeInTheDocument();
    expect(screen.getByLabelText("What to remove")).toBeInTheDocument();
    const table = screen.getByRole("table");
    expect(within(table).getAllByRole("row")).toHaveLength(3); // header + 2
    expect(within(table).getByText("(480) 555-0142")).toBeInTheDocument();
    expect(within(table).getByText("email of 3 Oct")).toBeInTheDocument();
    expect(within(table).getByText("Mesa, AZ")).toBeInTheDocument();
  });

  it("sends a member away before loading anything", async () => {
    vi.spyOn(api, "getAccountMe").mockResolvedValue({ ...mockAccountMe, role: "MEMBER" });
    const list = vi.spyOn(api, "getRemovals");
    await expect(AdminRemovalsPage()).rejects.toThrow("REDIRECT:/app");
    expect(list).not.toHaveBeenCalled();
  });

  it("is linked in the sidebar for a super admin only", () => {
    const { unmount } = render(<Sidebar role="SUPER_ADMIN" />);
    expect(screen.getByRole("link", { name: "Removals" })).toHaveAttribute("href", "/app/admin/removals");
    unmount();
    render(<Sidebar role="MEMBER" />);
    expect(screen.queryByRole("link", { name: "Removals" })).not.toBeInTheDocument();
  });
});

describe("RemovalForm", () => {
  it("cannot remove before the matches have been checked", () => {
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    expect(removeButton()).toBeDisabled();
  });

  it("shows how many permits match, and where, before it can remove", async () => {
    const preview = vi.spyOn(api, "previewRemoval");
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));

    expect(await screen.findByText("This matches 3 permits: Mesa, AZ 2, Austin, TX 1.")).toBeInTheDocument();
    expect(preview).toHaveBeenCalledWith("PHONE", "(480) 555-0142", "mock-token");
    expect(removeButton()).toBeEnabled();
  });

  it("says so when nothing stored matches, and still lets the value be listed", async () => {
    vi.spyOn(api, "previewRemoval").mockResolvedValue({ permits: 0, cities: [] });
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));

    expect(await screen.findByText(/Nothing stored matches/)).toBeInTheDocument();
    expect(removeButton()).toBeEnabled();
  });

  it("forgets the matches when the value or the kind changes", async () => {
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);

    fill("Phone number", "(480) 555-0143");
    expect(screen.queryByText(/This matches/)).not.toBeInTheDocument();
    expect(removeButton()).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    choose("EMAIL");
    expect(screen.queryByText(/This matches/)).not.toBeInTheDocument();
    expect(screen.getByLabelText("Email address")).toHaveValue("");
  });

  it("sends the count the admin saw, then reloads the list", async () => {
    const create = vi.spyOn(api, "createRemoval");
    render(<RemovalForm markets={markets} />);
    choose("NAME");
    fill("Name", "Jane Doe");
    fill("Note", "email of 3 Oct");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    fireEvent.click(removeButton());

    await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    expect(create).toHaveBeenCalledExactlyOnceWith(
      { kind: "NAME", value: "Jane Doe", note: "email of 3 Oct", confirmedCount: 3 }, "mock-token");
    expect(toast.success).toHaveBeenCalled();
    expect(screen.getByLabelText("Name")).toHaveValue("");
    expect(screen.queryByText(/This matches/)).not.toBeInTheDocument();
  });

  it("asks for a new check when the count changed", async () => {
    vi.spyOn(api, "createRemoval").mockRejectedValue(new api.ApiError("count_changed", 409));
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    fireEvent.click(removeButton());

    expect(await screen.findByRole("alert")).toHaveTextContent("The number of matches changed. Check the matches again.");
    expect(removeButton()).toBeDisabled();
    expect(refresh).not.toHaveBeenCalled();
  });

  it("says when the value is already on the list", async () => {
    vi.spyOn(api, "createRemoval").mockRejectedValue(new api.ApiError("removal_exists", 409));
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    fireEvent.click(removeButton());
    expect(await screen.findByRole("alert")).toHaveTextContent("This is already on the list.");
  });

  it("says what is wrong with a value the API refuses", async () => {
    vi.spyOn(api, "previewRemoval").mockRejectedValue(new api.ApiError("invalid_value", 400));
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "555");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("A phone number needs at least 10 digits.");
  });

  it("finds a record in a market and removes the one that is picked", async () => {
    const search = vi.spyOn(api, "searchRemovalRecords");
    const create = vi.spyOn(api, "createRemoval");
    render(<RemovalForm markets={markets} />);
    choose("RECORD");
    fireEvent.change(screen.getByLabelText("Market"), { target: { value: markets[1].slug } });
    fill("Permit number or address", "1 Main");
    fireEvent.click(screen.getByRole("button", { name: "Search" }));

    const pick = await screen.findByRole("button", { name: "Pick BLD-2026-0117" });
    expect(search).toHaveBeenCalledWith(markets[1].slug, "1 Main", "mock-token");
    expect(removeButton()).toBeDisabled();
    fireEvent.click(pick);
    expect(screen.getByText("This removes 1 permit: BLD-2026-0117, 1 Main St, Mesa, AZ.")).toBeInTheDocument();
    fireEvent.click(removeButton());

    await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    expect(create).toHaveBeenCalledExactlyOnceWith(
      { kind: "RECORD", permitId: "per-001", confirmedCount: 1 }, "mock-token");
  });

  it("says when no record is found", async () => {
    vi.spyOn(api, "searchRemovalRecords").mockResolvedValue([]);
    render(<RemovalForm markets={markets} />);
    choose("RECORD");
    fill("Permit number or address", "nothing here");
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(await screen.findByText("No permit in this market matches.")).toBeInTheDocument();
  });
});

describe("RemovalTable", () => {
  it("says when the list is empty", () => {
    render(<RemovalTable removals={[]} />);
    expect(screen.getByText("Nothing has been removed yet.")).toBeInTheDocument();
  });

  it("asks before an undo and says that nothing comes back by itself", async () => {
    const undo = vi.spyOn(api, "undoRemoval");
    render(<RemovalTable removals={mockRemovals} />);
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal of (480) 555-0142" }));

    expect(undo).not.toHaveBeenCalled();
    expect(screen.getByText(/does not put anything back/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByText(/does not put anything back/)).not.toBeInTheDocument();
    expect(undo).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Undo the removal of (480) 555-0142" }));
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal" }));
    await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    expect(undo).toHaveBeenCalledExactlyOnceWith("rem-001", "mock-token");
  });

  it("keeps the row and says so when the undo fails", async () => {
    vi.spyOn(api, "undoRemoval").mockRejectedValue(new Error("down"));
    render(<RemovalTable removals={mockRemovals} />);
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal of (480) 555-0142" }));
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal" }));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.getByText("(480) 555-0142")).toBeInTheDocument();
    expect(refresh).not.toHaveBeenCalled();
  });
});
