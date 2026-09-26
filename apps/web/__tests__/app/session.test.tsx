// @vitest-environment jsdom
import "./dom-cleanup";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, renderHook, screen, waitFor } from "@testing-library/react";

const auth = vi.hoisted(() => ({
  currentUser: null as null | { getIdToken: () => Promise<string> },
  authStateReady: vi.fn(async () => undefined),
}));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: auth }));
vi.mock("firebase/auth", () => ({ signOut: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }) }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));
vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()),
  saveLead: vi.fn(),
  unsaveLead: vi.fn(),
}));

import { signOut } from "firebase/auth";
import { toast } from "sonner";
import { ApiError, saveLead } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { SessionExpiredError, reportMutationError } from "@/components/app/sign-out";
import { SessionRecoveryCard } from "@/components/app/session-recovery";
import { SaveButton } from "@/components/app/lead-detail/save-button";
import AppError from "@/app/app/error";
import { SESSION_EXPIRED_DIGEST } from "@/components/app/session-digest";

let calls: string[];
let replace: ReturnType<typeof vi.fn>;

beforeEach(() => {
  vi.clearAllMocks();
  vi.unstubAllEnvs();
  auth.currentUser = null;
  calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url: string) => { calls.push(`fetch ${url}`); return { ok: true }; }));
  vi.mocked(signOut).mockImplementation(async () => { calls.push("firebase signOut"); });
  replace = vi.fn((url: string) => { calls.push(`replace ${url}`); });
  vi.stubGlobal("location", { ...window.location, replace });
});
afterEach(() => vi.unstubAllGlobals());

const SIGN_OUT_SEQUENCE = ["fetch /api/logout", "firebase signOut", "replace /login"];

describe("useApiToken", () => {
  it("returns the signed-in user's ID token", async () => {
    auth.currentUser = { getIdToken: async () => "id-token" };
    const { result } = renderHook(() => useApiToken());
    await expect(result.current()).resolves.toBe("id-token");
    expect(fetch).not.toHaveBeenCalled();
  });

  it("never returns an empty bearer: with no user it expires the session and throws", async () => {
    const { result } = renderHook(() => useApiToken());
    await expect(result.current()).rejects.toBeInstanceOf(SessionExpiredError);
    expect(auth.authStateReady).toHaveBeenCalled();
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(toast.error).toHaveBeenCalledWith("Your session expired. Please sign in again.");
    expect(calls).toEqual(SIGN_OUT_SEQUENCE);
  });
});

describe("reportMutationError", () => {
  it("runs the sign-out flow once for concurrent 401s", async () => {
    reportMutationError(new ApiError("Unauthorized", 401), "Could not save");
    reportMutationError(new ApiError("Unauthorized", 401), "Could not save");
    await waitFor(() => expect(replace).toHaveBeenCalledTimes(1));
    expect(calls).toEqual(SIGN_OUT_SEQUENCE);
    expect(toast.error).toHaveBeenCalledTimes(1);
    expect(toast.error).not.toHaveBeenCalledWith("Could not save");
  });

  it("shows the given message for other failures and keeps the session", () => {
    reportMutationError(new ApiError("boom", 500), "Could not save");
    expect(toast.error).toHaveBeenCalledWith("Could not save");
    expect(fetch).not.toHaveBeenCalled();
  });
});

describe("mutation callers", () => {
  it("SaveButton signs the user out when the API answers 401", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1");
    vi.mocked(saveLead).mockRejectedValue(new ApiError("Unauthorized", 401));
    render(<SaveButton leadId="lead-001" savedId={null} />);
    fireEvent.click(screen.getByRole("button", { name: "Save lead" }));
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(calls).toEqual(SIGN_OUT_SEQUENCE);
    expect(toast.error).toHaveBeenCalledWith("Your session expired. Please sign in again.");
    expect(toast.error).not.toHaveBeenCalledWith("Could not update saved leads");
    expect(screen.getByRole("button", { name: "Save lead" })).toBeInTheDocument();
  });

  it("SaveButton never calls the API without a token", async () => {
    render(<SaveButton leadId="lead-001" savedId={null} />);
    fireEvent.click(screen.getByRole("button", { name: "Save lead" }));
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(saveLead).not.toHaveBeenCalled();
  });
});

describe("session recovery UI", () => {
  it("'Sign in again' clears the cookie, then Firebase, then replaces the page with /login", async () => {
    render(<SessionRecoveryCard />);
    fireEvent.click(screen.getByRole("button", { name: "Sign in again" }));
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
    expect(calls).toEqual(SIGN_OUT_SEQUENCE);
  });

  it("does not sign out of Firebase if clearing the cookie fails", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => ({ ok: false, status: 500 })));
    render(<SessionRecoveryCard />);
    fireEvent.click(screen.getByRole("button", { name: "Sign in again" }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Could not sign out. Please try again."));
    expect(signOut).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Sign in again" })).toBeEnabled();
  });

  it("app/app/error.tsx shows session recovery for tagged 401s", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    const error = Object.assign(new Error("redacted"), { digest: SESSION_EXPIRED_DIGEST });
    render(<AppError error={error} reset={vi.fn()} />);
    expect(screen.getByText("We couldn’t verify your session")).toBeInTheDocument();
  });

  it("app/app/error.tsx offers retry for other failures", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    const reset = vi.fn();
    render(<AppError error={Object.assign(new Error("x"), { digest: "123" })} reset={reset} />);
    expect(screen.getByText("Something went wrong loading this page")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(reset).toHaveBeenCalled();
  });
});
