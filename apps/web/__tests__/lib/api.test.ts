import { beforeEach, describe, expect, it, vi } from "vitest";

const fixtureLeads = {
  items: [], total: 0, page: 1, pageSize: 25,
  freshness: { lastUpdatedAt: null },
};
const fixtureMarkets = [
  { id: "m_1", name: "Houston", city: "Houston", state: "TX", slug: "houston-tx" },
];

vi.mock("@/lib/fixtures", () => ({
  getLeads: vi.fn(async () => fixtureLeads),
  submitSampleLeadRequest: vi.fn(async () => undefined),
}));

vi.mock("@/lib/fixtures/markets", () => ({
  mockMarkets: fixtureMarkets,
  mockMarketStats: {},
}));

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("lib/api", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it("returns fixture data when NEXT_PUBLIC_API_MOCK=1 without calling fetch", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1");
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const api = await import("@/lib/api");

    const markets = await api.getMarkets();
    const leads = await api.getLeads({ market: "houston-tx" }, "tok_ignored");

    expect(markets).toEqual(fixtureMarkets);
    expect(leads).toEqual(fixtureLeads);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("resolves sample-lead requests in mock mode without calling fetch", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1");
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const api = await import("@/lib/api");

    await expect(
      api.submitSampleLeadRequest({ name: "Jo", email: "jo@example.com", company: "Jo Co", marketSlug: "houston-tx" }),
    ).resolves.toBeUndefined();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("calls the real API with base URL, query params, and bearer token when mock is off", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
    const fetchMock = vi.fn<typeof fetch>(async () => jsonResponse(fixtureLeads));
    vi.stubGlobal("fetch", fetchMock);
    const api = await import("@/lib/api");

    await api.getLeads({ market: "houston-tx", minScore: 70, page: 2, pageSize: 10 }, "tok_123");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("http://api.test/api/leads?market=houston-tx&minScore=70&page=2&pageSize=10");
    expect(new Headers(init.headers).get("Authorization")).toBe("Bearer tok_123");
  });

  it("sends JSON bodies for mutating calls", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
    const fetchMock = vi.fn<typeof fetch>(async () => jsonResponse({ id: "sl_1", status: "SAVED", createdAt: "2026-08-19T00:00:00Z", lead: null }));
    vi.stubGlobal("fetch", fetchMock);
    const api = await import("@/lib/api");

    await api.saveLead("op_1", "tok_123");

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("http://api.test/api/saved-leads");
    expect(init.method).toBe("POST");
    expect(init.body).toBe(JSON.stringify({ fireOpportunityId: "op_1" }));
    expect(new Headers(init.headers).get("Content-Type")).toBe("application/json");
  });

  it("throws a typed ApiError with the API-provided message and status on non-2xx responses", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({ error: "Lead not found" }, 404)));
    const api = await import("@/lib/api");

    await expect(api.getLead("missing", "tok_123")).rejects.toThrow("Lead not found");
    await expect(api.getLead("missing", "tok_123")).rejects.toMatchObject({ name: "ApiError", status: 404 });
  });

  it("encodes path parameters containing reserved characters", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
    const fetchMock = vi.fn<typeof fetch>(async () => jsonResponse({}));
    vi.stubGlobal("fetch", fetchMock);
    const api = await import("@/lib/api");

    await api.getLead("a/b", "tok_123");

    const [url] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("http://api.test/api/leads/a%2Fb");
  });

  it("resolves void for 204 responses", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);
    const api = await import("@/lib/api");

    await expect(api.unsaveLead("sl_1", "tok_123")).resolves.toBeUndefined();
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("http://api.test/api/saved-leads/sl_1");
    expect(init.method).toBe("DELETE");
  });

  it("maps setSourceActive to the enable/disable admin routes", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "0");
    vi.stubEnv("NEXT_PUBLIC_API_URL", "http://api.test");
    const fetchMock = vi.fn<typeof fetch>(async () => new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const api = await import("@/lib/api");

    await api.setSourceActive("src_1", false, "tok_admin");
    await api.setSourceActive("src_1", true, "tok_admin");

    expect(fetchMock.mock.calls[0][0]).toBe("http://api.test/api/admin/sources/src_1/disable");
    expect(fetchMock.mock.calls[1][0]).toBe("http://api.test/api/admin/sources/src_1/enable");
  });
});
