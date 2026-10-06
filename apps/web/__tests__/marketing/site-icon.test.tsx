import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { FlameMark } from "@/components/marketing/site-nav";

const appDir = join(__dirname, "..", "..", "app");
// The favicon that create-next-app ships: the black triangle shown in browser tabs.
const NEXT_DEFAULT_FAVICON_SHA256 = "2b8ad2d33455a8f736fc3a8ebf8f0bdea8848ad4c0db48a2833bd0f9cd775932";

describe("site icon", () => {
  it("draws the same flame as the logo, in the brand orange", () => {
    const { container } = render(<FlameMark />);
    const logoPath = container.querySelector("path")?.getAttribute("d");
    const icon = readFileSync(join(appDir, "icon.svg"), "utf8");

    expect(logoPath).toBeTruthy();
    expect(icon).toContain(`d="${logoPath}"`);
    expect(icon).toContain("#f97316");
  });

  it("no longer serves the Next.js default favicon", () => {
    const ico = readFileSync(join(appDir, "favicon.ico"));
    expect(createHash("sha256").update(ico).digest("hex")).not.toBe(NEXT_DEFAULT_FAVICON_SHA256);
  });

  it("gives iOS home screens a flame icon too", () => {
    expect(existsSync(join(appDir, "apple-icon.png"))).toBe(true);
  });
});
