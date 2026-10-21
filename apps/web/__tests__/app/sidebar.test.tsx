// @vitest-environment jsdom
import "./dom-cleanup";
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { Sidebar } from "@/components/app/sidebar";

vi.mock("next/navigation", () => ({
  usePathname: () => "/app/leads",
}));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "tok" }));

describe("Sidebar", () => {
  it("renders the six member nav items", () => {
    render(<Sidebar role="MEMBER" />);
    for (const label of ["Overview", "Leads", "Saved", "Alerts", "Markets", "Account"]) {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    }
  });

  it("hides the admin section for MEMBER and ADMIN roles", () => {
    const { rerender } = render(<Sidebar role="MEMBER" />);
    expect(screen.queryByText("Admin")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Sources" })).not.toBeInTheDocument();
    rerender(<Sidebar role="ADMIN" />);
    expect(screen.queryByRole("link", { name: "Sources" })).not.toBeInTheDocument();
  });

  it("shows Sources, Runs, Users, Subscriptions for SUPER_ADMIN", () => {
    render(<Sidebar role="SUPER_ADMIN" />);
    expect(screen.getByRole("link", { name: "Sources" })).toHaveAttribute("href", "/app/admin/sources");
    expect(screen.getByRole("link", { name: "Runs" })).toHaveAttribute("href", "/app/admin/runs");
    expect(screen.getByRole("link", { name: "Pre-sprinkler" })).toHaveAttribute("href", "/app/admin/pre-sprinkler");
    expect(screen.getByRole("link", { name: "Users" })).toHaveAttribute("href", "/app/admin/users");
    expect(screen.getByRole("link", { name: "Subscriptions" })).toHaveAttribute("href", "/app/admin/subscriptions");
  });

  it("links super admins to the lead tags report", () => {
    render(<Sidebar role="SUPER_ADMIN" />);
    expect(screen.getByRole("link", { name: "Lead tags" })).toHaveAttribute("href", "/app/admin/outcomes");
  });

  it("marks the active route with the orange accent", () => {
    render(<Sidebar role="MEMBER" />);
    const active = screen.getByRole("link", { name: "Leads" });
    expect(active.className).toContain("text-orange-600");
    expect(screen.getByRole("link", { name: "Saved" }).className).not.toContain("text-orange-600");
  });
});

describe("Sidebar extras", () => {
  it("marks the active route with aria-current and calls onNavigate on click", () => {
    const onNavigate = vi.fn();
    render(<Sidebar role="MEMBER" onNavigate={onNavigate} />);
    expect(screen.getByRole("link", { name: "Leads" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Saved" })).not.toHaveAttribute("aria-current");
    screen.getByRole("link", { name: "Saved" }).click();
    expect(onNavigate).toHaveBeenCalled();
  });

  it("shows the upgrade card only below the Territory plan", () => {
    const { rerender } = render(<Sidebar role="MEMBER" plan="PRO" />);
    expect(screen.getByRole("link", { name: /Upgrade plan/ })).toHaveAttribute("href", "/app/account");
    rerender(<Sidebar role="MEMBER" plan="TERRITORY" />);
    expect(screen.queryByRole("link", { name: /Upgrade plan/ })).not.toBeInTheDocument();
  });

  it("hides the upgrade card during the free pilot, whatever the plan", () => {
    const { rerender } = render(<Sidebar role="MEMBER" plan={null} freeAccess />);
    expect(screen.queryByText("Unlock more markets")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Upgrade plan/ })).not.toBeInTheDocument();
    rerender(<Sidebar role="MEMBER" plan="PRO" freeAccess />);
    expect(screen.queryByRole("link", { name: /Upgrade plan/ })).not.toBeInTheDocument();
  });
  it("puts Send feedback below the main nav for every role", () => {
    for (const role of ["MEMBER", "ADMIN", "SUPER_ADMIN"] as const) {
      const { unmount } = render(<Sidebar role={role} />);
      const button = screen.getByRole("button", { name: "Send feedback" });
      const lastNav = screen.getByRole("link", { name: "Account" });
      expect(lastNav.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      unmount();
    }
  });

  it("opens the feedback dialog itself when no handler is given", () => {
    render(<Sidebar role="MEMBER" />);
    fireEvent.click(screen.getByRole("button", { name: "Send feedback" }));
    expect(screen.getByRole("dialog", { name: "Send feedback" })).toBeInTheDocument();
  });

  it("hands Send feedback to the given handler instead of opening its own dialog", () => {
    const onSendFeedback = vi.fn();
    render(<Sidebar role="MEMBER" onSendFeedback={onSendFeedback} />);
    fireEvent.click(screen.getByRole("button", { name: "Send feedback" }));
    expect(onSendFeedback).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
