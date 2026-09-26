import { describe, expect, it, vi } from "vitest";

vi.mock("next-firebase-auth-edge", () => ({
  authMiddleware: vi.fn(),
  redirectToLogin: vi.fn(),
}));

import { config } from "@/middleware";

describe("middleware matcher config", () => {
  it("covers the dashboard surface and the login/logout routes", () => {
    expect(config.matcher).toContain("/app/:path*");
    expect(config.matcher).toContain("/api/login");
    expect(config.matcher).toContain("/api/logout");
  });
});
