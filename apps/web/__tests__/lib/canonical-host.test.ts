import { describe, expect, it } from "vitest";
import nextConfig from "../../next.config";
import { CANONICAL_HOST, WWW_HOST, canonicalHostRedirects } from "@/lib/canonical-host";
import { SITE_URL } from "@/lib/seo";

/** Mirrors how Next.js evaluates a `has` host condition: the value is an anchored regex. */
function hostMatches(rule: { has?: { type: string; value?: string }[] }, host: string): boolean {
  return (rule.has ?? []).every(
    (h) => h.type === "host" && h.value !== undefined && new RegExp(`^${h.value}$`).test(host),
  );
}

describe("canonical host", () => {
  it("is the host of SITE_URL, so redirects, canonicals and the sitemap agree", () => {
    expect(CANONICAL_HOST).toBe(new URL(SITE_URL).host);
    expect(CANONICAL_HOST).toBe("permittorch.com");
    expect(WWW_HOST).toBe("www.permittorch.com");
  });

  it("permanently redirects every www path to the same path on the apex", () => {
    const [rule, ...rest] = canonicalHostRedirects();
    expect(rest).toEqual([]);
    expect(rule.source).toBe("/:path*");
    expect(rule.destination).toBe("https://permittorch.com/:path*");
    expect(rule.permanent).toBe(true);
    expect(hostMatches(rule, "www.permittorch.com")).toBe(true);
  });

  it("leaves the apex, the Railway domain, localhost and lookalike hosts alone", () => {
    const [rule] = canonicalHostRedirects();
    for (const host of [
      "permittorch.com",
      "web-production-2b8db2.up.railway.app",
      "localhost:3000",
      "wwwxpermittorch.com",
      "www.permittorch.com.evil.example",
      "evil.www.permittorch.com",
    ]) {
      expect(hostMatches(rule, host), host).toBe(false);
    }
  });

  it("is wired into next.config.ts", async () => {
    expect(await nextConfig.redirects?.()).toEqual(canonicalHostRedirects());
  });
});
