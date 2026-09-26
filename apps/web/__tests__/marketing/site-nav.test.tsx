// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";

let pathname: string | null = "/";
vi.mock("next/navigation", () => ({ usePathname: () => pathname }));

import { SiteNav, isActivePath } from "@/components/marketing/site-nav";

// vitest.config.mts does not set `test.globals: true`, so
// @testing-library/react's afterEach-based auto-cleanup never registers.
// Clean up explicitly so each render() starts from an empty DOM.
afterEach(() => {
  cleanup();
  pathname = "/";
});

describe("SiteNav", () => {
  it("renders the wordmark linking home", () => {
    render(<SiteNav />);
    const home = screen.getByRole("link", { name: /permittorch/i });
    expect(home).toHaveAttribute("href", "/");
  });

  it.each([
    ["Leads", "/fire-protection-leads"],
    ["Markets", "/locations"],
    ["How It Works", "/how-it-works"],
    ["Pricing", "/pricing"],
    ["Resources", "/blog"],
  ])("links %s to %s", (label, href) => {
    render(<SiteNav />);
    const links = screen.getAllByRole("link", { name: label });
    expect(links.some((l) => l.getAttribute("href") === href)).toBe(true);
  });

  it("renders Login and Start Free actions", () => {
    render(<SiteNav />);
    expect(screen.getAllByRole("link", { name: "Login" })[0]).toHaveAttribute("href", "/login");
    expect(screen.getAllByRole("link", { name: "Start Free" })[0]).toHaveAttribute("href", "/signup");
  });

  it("toggles the mobile menu with aria-expanded and aria-controls", () => {
    const { container } = render(<SiteNav />);
    const toggle = screen.getByRole("button", { name: "Toggle menu" });
    const menu = container.querySelector(`#${CSS.escape(toggle.getAttribute("aria-controls")!)}`)!;
    expect(menu).not.toBeNull();
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(menu).toHaveAttribute("hidden");
    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(menu).not.toHaveAttribute("hidden");
    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "false");
  });

  it("closes the mobile menu on Escape and returns focus to the toggle", () => {
    render(<SiteNav />);
    const toggle = screen.getByRole("button", { name: "Toggle menu" });
    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    fireEvent.keyDown(document, { key: "Escape" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(document.activeElement).toBe(toggle);
  });

  it("hides decorative SVGs from assistive tech", () => {
    const { container } = render(<SiteNav />);
    for (const svg of container.querySelectorAll("svg")) expect(svg).toHaveAttribute("aria-hidden", "true");
  });

  it("marks the active section with aria-current=page", () => {
    pathname = "/blog/using-building-permits-for-lead-generation";
    render(<SiteNav />);
    const resources = screen.getAllByRole("link", { name: "Resources" })[0];
    expect(resources).toHaveAttribute("aria-current", "page");
    expect(screen.getAllByRole("link", { name: "Pricing" })[0]).not.toHaveAttribute("aria-current");
  });

  it("isActivePath matches exact and nested paths only", () => {
    expect(isActivePath("/pricing", "/pricing")).toBe(true);
    expect(isActivePath("/blog/x", "/blog")).toBe(true);
    expect(isActivePath("/blogger", "/blog")).toBe(false);
    expect(isActivePath(null, "/blog")).toBe(false);
  });
});
