import { describe, expect, it } from "vitest";
import { isPublicPath, isAuthPage } from "@/lib/auth/config";

describe("locked public routes", () => {
  it.each([
    "/", "/pricing", "/how-it-works", "/fire-protection-leads", "/fire-protection-leads/texas",
    "/fire-sprinkler-leads", "/fire-alarm-leads", "/locations", "/locations/texas/austin",
    "/blog", "/blog/post-1", "/login", "/login/reset", "/signup", "/api/login", "/api/logout", "/api/anything",
  ])("treats %s as public", (path) => {
    expect(isPublicPath(path)).toBe(true);
  });

  it.each(["/app", "/app/leads", "/app/leads/abc", "/app/saved", "/app/admin/sources", "/pricing/secret", "/fire-sprinkler-leads/x"])(
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
