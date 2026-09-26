import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AccountMe } from "@permittorch/types";

const redirect = vi.fn((path: string) => { throw new Error(`REDIRECT:${path}`); });
vi.mock("next/navigation", () => ({ redirect: (p: string) => redirect(p) }));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("@/lib/api", () => ({ getAccountMe: vi.fn() }));

import { getAccountMe } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";

const me = (role: AccountMe["role"]): AccountMe => ({
  email: "x@y.com", role, organizationName: "Org", plan: "PRO", digestFrequency: "DAILY",
});

beforeEach(() => vi.clearAllMocks());

describe("requireSuperAdmin", () => {
  it("returns the token for SUPER_ADMIN", async () => {
    vi.mocked(getAccountMe).mockResolvedValue(me("SUPER_ADMIN"));
    await expect(requireSuperAdmin()).resolves.toBe("mock-token");
    expect(redirect).not.toHaveBeenCalled();
  });

  it("redirects MEMBER and ADMIN to /app", async () => {
    vi.mocked(getAccountMe).mockResolvedValue(me("MEMBER"));
    await expect(requireSuperAdmin()).rejects.toThrow("REDIRECT:/app");
    vi.mocked(getAccountMe).mockResolvedValue(me("ADMIN"));
    await expect(requireSuperAdmin()).rejects.toThrow("REDIRECT:/app");
    expect(redirect).toHaveBeenCalledTimes(2);
  });
});
