// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { SiteNav } from "@/components/marketing/site-nav";

// vitest.config.mts does not set `test.globals: true`, so
// @testing-library/react's afterEach-based auto-cleanup never registers.
// Clean up explicitly so each render() starts from an empty DOM.
afterEach(() => cleanup());

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
});
