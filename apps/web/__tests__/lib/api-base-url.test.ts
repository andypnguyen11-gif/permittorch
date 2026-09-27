import { afterEach, describe, expect, it, vi } from "vitest";
import {
  DEV_API_URL,
  MISSING_API_URL_MESSAGE,
  assertApiUrlConfigured,
  resolveApiBaseUrl,
} from "@/lib/api-base-url";
import { apiFetch } from "@/lib/api";

describe("resolveApiBaseUrl", () => {
  it("uses the configured URL in every environment", () => {
    expect(resolveApiBaseUrl({ NEXT_PUBLIC_API_URL: "https://api.example", NODE_ENV: "production" }))
      .toBe("https://api.example");
    expect(resolveApiBaseUrl({ NEXT_PUBLIC_API_URL: "https://api.example", NODE_ENV: "development" }))
      .toBe("https://api.example");
  });

  it("falls back to the local API only outside production", () => {
    expect(resolveApiBaseUrl({ NODE_ENV: "development" })).toBe(DEV_API_URL);
    expect(resolveApiBaseUrl({ NODE_ENV: "test" })).toBe(DEV_API_URL);
  });

  it.each([undefined, "", "   "])("throws in production when the URL is %j", (url) => {
    expect(() => resolveApiBaseUrl({ NEXT_PUBLIC_API_URL: url, NODE_ENV: "production" }))
      .toThrow(MISSING_API_URL_MESSAGE);
  });
});

describe("assertApiUrlConfigured (next.config build guard)", () => {
  it("fails a production build without NEXT_PUBLIC_API_URL", () => {
    expect(() => assertApiUrlConfigured({ NODE_ENV: "production" })).toThrow(MISSING_API_URL_MESSAGE);
  });

  it("allows production mock builds, configured builds, and dev", () => {
    expect(() => assertApiUrlConfigured({ NODE_ENV: "production", NEXT_PUBLIC_API_MOCK: "1" })).not.toThrow();
    expect(() => assertApiUrlConfigured({ NODE_ENV: "production", NEXT_PUBLIC_API_URL: "https://api.example" }))
      .not.toThrow();
    expect(() => assertApiUrlConfigured({ NODE_ENV: "development" })).not.toThrow();
  });
});

describe("apiFetch base URL in production", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it("never calls localhost when a production bundle lacks NEXT_PUBLIC_API_URL", async () => {
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "");
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    await expect(apiFetch("/api/markets")).rejects.toThrow(MISSING_API_URL_MESSAGE);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
