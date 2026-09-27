// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "@testing-library/react";

vi.mock("@/lib/analytics", () => ({
  identifyUser: vi.fn(), initAnalytics: vi.fn(), resetAnalyticsUser: vi.fn(),
}));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
type Listener = (user: unknown) => void;
let listener: Listener | null = null;
vi.mock("firebase/auth", () => ({
  onAuthStateChanged: (_auth: unknown, cb: Listener) => { listener = cb; return () => {}; },
}));

import { identifyUser, initAnalytics, resetAnalyticsUser } from "@/lib/analytics";
import { AnalyticsIdentity } from "@/components/app/analytics-identity";

beforeEach(() => { vi.clearAllMocks(); listener = null; });

describe("AnalyticsIdentity", () => {
  it("identifies by the internal user id, never the Firebase uid, email or name", () => {
    render(<AnalyticsIdentity userId="0b7c5c3e-internal" />);
    expect(initAnalytics).toHaveBeenCalledTimes(1);
    listener!({ uid: "firebase-uid-42", email: "person@example.com", displayName: "Pat Person" });
    expect(vi.mocked(identifyUser).mock.calls).toEqual([["0b7c5c3e-internal"]]);
  });

  it("resets the analytics user on sign-out", () => {
    render(<AnalyticsIdentity userId="0b7c5c3e-internal" />);
    listener!(null);
    expect(resetAnalyticsUser).toHaveBeenCalledTimes(1);
  });
});
