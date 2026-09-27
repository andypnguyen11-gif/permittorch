import { describe, expect, it } from "vitest";
import { isPublicPath, isAuthPage } from "@/lib/auth/config";

describe("locked public routes", () => {
  it.each([
    "/", "/pricing", "/how-it-works", "/fire-protection-leads", "/fire-protection-leads/texas",
    "/fire-sprinkler-leads", "/fire-alarm-leads", "/locations", "/locations/texas/austin",
    "/blog", "/blog/post-1", "/login", "/login/reset", "/signup", "/api/login", "/api/logout", "/api/anything",
    "/terms", "/privacy",
    "/opengraph-image", "/opengraph-image-abc123", "/icon", "/icon.png", "/icon0", "/apple-icon",
    "/apple-icon.png", "/og-image.png", "/icon-abc123.png", "/apple-icon1.png", "/opengraph-image.png",
    "/opengraph-image-abc123.jpg",
  ])("treats %s as public", (path) => {
    expect(isPublicPath(path)).toBe(true);
  });

  it.each([
    "/app", "/app/leads", "/app/leads/abc", "/app/saved", "/app/admin/sources", "/pricing/secret",
    "/fire-sprinkler-leads/x", "/terms/x", "/privacy/x", "/app/opengraph-image", "/app/icon",
    "/og-image.png/x", "/icon-library", "/icon-library/x", "/iconography", "/apple-icons",
    "/opengraph-images/x", "/opengraph-images", "/opengraph-image/x", "/icon/x",
  ])(
    "treats %s as protected",
    (path) => {
      expect(isPublicPath(path)).toBe(false);
    },
  );

  it("identifies the auth pages that signed-in users are bounced away from", () => {
    expect(isAuthPage("/login")).toBe(true);
    expect(isAuthPage("/signup")).toBe(true);
    expect(isAuthPage("/")).toBe(false);
    expect(isAuthPage("/app/leads")).toBe(false);
  });
});
