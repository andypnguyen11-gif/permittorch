import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AccountMe } from "@permittorch/types";

const redirect = vi.fn((path: string) => { throw new Error(`REDIRECT:${path}`); });
vi.mock("next/navigation", () => ({ redirect: (p: string) => redirect(p) }));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()), getAccountMe: vi.fn() }));

import { ApiError, getAccountMe } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { SESSION_EXPIRED_DIGEST } from "@/components/app/session-digest";

const me = (role: AccountMe["role"]): AccountMe => ({
  id: "u1", email: "x@y.com", role, organizationName: "Org", plan: "PRO", digestFrequency: "DAILY",
  hasLiveSubscription: true,
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

  it("routes a 401 through handleApiError (session recovery), not a /login redirect", async () => {
    vi.mocked(getAccountMe).mockRejectedValue(new ApiError("expired", 401));
    await expect(requireSuperAdmin()).rejects.toMatchObject({ status: 401, digest: SESSION_EXPIRED_DIGEST });
    expect(redirect).not.toHaveBeenCalled();
  });

  it("rethrows other API failures untagged", async () => {
    vi.mocked(getAccountMe).mockRejectedValue(new ApiError("boom", 500));
    const err = await requireSuperAdmin().catch((e: unknown) => e);
    expect(err).toMatchObject({ status: 500 });
    expect(err).not.toHaveProperty("digest");
  });
});
