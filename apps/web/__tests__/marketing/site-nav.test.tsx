// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SiteNav } from "@/components/marketing/site-nav";

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
