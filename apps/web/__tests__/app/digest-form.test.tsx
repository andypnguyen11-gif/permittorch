// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("@/lib/api", () => ({ updateEmailPreferences: vi.fn() }));
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

  it("reverts the selection when saving fails", async () => {
    vi.mocked(updateEmailPreferences).mockRejectedValue(new Error("boom"));
    render(<DigestForm initialFrequency="DAILY" />);
    fireEvent.click(screen.getByRole("radio", { name: /Off/ }));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.getByRole("radio", { name: /Daily/ })).toBeChecked();
  });
});
