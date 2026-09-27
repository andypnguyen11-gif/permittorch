import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const authMiddleware = vi.fn(async () => new Response("auth"));
const redirectToLogin = vi.fn(() => new Response(null, { status: 307, headers: { location: "/login" } }));
vi.mock("next-firebase-auth-edge", () => ({ authMiddleware, redirectToLogin }));
const captureException = vi.fn();
vi.mock("@sentry/nextjs", () => ({ captureException }));

const keys: string[] = [];
vi.mock("@/lib/auth/config", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/auth/config")>();
  return { ...actual, authConfig: { ...actual.authConfig, cookieSignatureKeys: keys } };
});

const req = (path: string) => new NextRequest(new URL(path, "http://localhost:3000"));

let warn: ReturnType<typeof vi.spyOn>;
beforeEach(() => {
  vi.resetModules();
  vi.clearAllMocks();
  keys.length = 0;
  warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
});
afterEach(() => warn.mockRestore());

describe("middleware without cookie signature keys", () => {
  it("serves public pages without calling authMiddleware", async () => {
    const { middleware } = await import("@/middleware");
    const res = await middleware(req("/pricing"));
    expect(res.headers.get("x-middleware-next")).toBe("1");
    expect(authMiddleware).not.toHaveBeenCalled();
    expect(redirectToLogin).not.toHaveBeenCalled();
  });

  it("redirects protected routes to /login instead of throwing", async () => {
    const { middleware } = await import("@/middleware");
    const res = await middleware(req("/app/leads"));
    expect(res.status).toBe(307);
    expect(redirectToLogin).toHaveBeenCalledWith(expect.anything(), { path: "/login", publicPaths: [] });
    expect(authMiddleware).not.toHaveBeenCalled();
  });

  it("warns once, not on every request", async () => {
    const { middleware } = await import("@/middleware");
    await middleware(req("/"));
    await middleware(req("/app"));
    await middleware(req("/login"));
    expect(warn).toHaveBeenCalledTimes(1);
    expect(String(warn.mock.calls[0][0])).toMatch(/AUTH_COOKIE_SIGNATURE_KEY/);
  });
});

describe("middleware with cookie signature keys", () => {
  it("delegates to authMiddleware", async () => {
    keys.push("k1");
    const { middleware } = await import("@/middleware");
    await middleware(req("/app/leads"));
    expect(authMiddleware).toHaveBeenCalledTimes(1);
    expect(warn).not.toHaveBeenCalled();
  });
});

describe("middleware handleError", () => {
  type Options = { handleError: (error: unknown) => Promise<Response> };
  const handleErrorFor = async (path: string) => {
    keys.push("k1");
    const { middleware } = await import("@/middleware");
    await middleware(req(path));
    return (authMiddleware.mock.calls[0] as unknown as [NextRequest, Options])[1].handleError;
  };

  it("reports the token error to Sentry and still redirects protected routes", async () => {
    const error = new Error("invalid token");
    const res = await (await handleErrorFor("/app/leads"))(error);
    expect(captureException).toHaveBeenCalledWith(error);
    expect(res.status).toBe(307);
  });

  it("reports the token error to Sentry and still serves public pages", async () => {
    const error = new Error("bad signature");
    const res = await (await handleErrorFor("/pricing"))(error);
    expect(captureException).toHaveBeenCalledWith(error);
    expect(res.headers.get("x-middleware-next")).toBe("1");
  });
});

describe("middleware handleValidToken on /login and /signup", () => {
  type Options = { handleValidToken: (tokens: unknown, headers: Headers) => Promise<Response> };
  const redirectFor = async (path: string) => {
    keys.push("k1");
    const { middleware } = await import("@/middleware");
    const request = req(path);
    await middleware(request);
    const options = (authMiddleware.mock.calls[0] as unknown as [NextRequest, Options])[1];
    const res = await options.handleValidToken({}, new Headers());
    return res.headers.get("location");
  };

  it.each([
    ["/signup?plan=territory", "http://localhost:3000/app/account?plan=TERRITORY"],
    ["/login?plan=STARTER", "http://localhost:3000/app/account?plan=STARTER"],
    ["/signup?plan=gold", "http://localhost:3000/app/leads"],
    ["/login", "http://localhost:3000/app/leads"],
  ])("sends a signed-in visitor of %s to %s", async (path, location) => {
    expect(await redirectFor(path)).toBe(location);
  });

  it("does not redirect signed-in visitors of other pages", async () => {
    keys.push("k1");
    const { middleware } = await import("@/middleware");
    await middleware(req("/pricing?plan=PRO"));
    const options = (authMiddleware.mock.calls[0] as unknown as [NextRequest, Options])[1];
    const res = await options.handleValidToken({}, new Headers());
    expect(res.headers.get("x-middleware-next")).toBe("1");
  });
});
