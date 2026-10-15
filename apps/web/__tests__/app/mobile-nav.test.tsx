// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("next/navigation", () => ({
  usePathname: () => "/app/leads",
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "tok" }));

import { MobileNav } from "@/components/app/mobile-nav";

describe("MobileNav", () => {
  it("closes the menu and keeps the feedback dialog open", async () => {
    render(<MobileNav role="MEMBER" />);
    fireEvent.click(screen.getByRole("button", { name: "Open navigation" }));
    fireEvent.click(await screen.findByRole("button", { name: "Send feedback" }));

    await waitFor(() => expect(screen.queryByText("Navigation")).not.toBeInTheDocument());
    expect(screen.getByRole("dialog", { name: "Send feedback" })).toBeInTheDocument();
  });
});
