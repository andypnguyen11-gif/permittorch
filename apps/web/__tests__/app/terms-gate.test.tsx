// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

const { refresh, acceptTerms, signOutAndRedirect } = vi.hoisted(() => ({
  refresh: vi.fn(),
  acceptTerms: vi.fn(),
  signOutAndRedirect: vi.fn(),
}));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
vi.mock("firebase/auth", () => ({ signOut: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "token-1" }));
vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()),
  acceptTerms,
}));
vi.mock("@/components/app/sign-out", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/components/app/sign-out")>()),
  signOutAndRedirect,
}));

import { ApiError } from "@/lib/api";
import { TERMS_VERSION } from "@/lib/terms";
import { TermsGate } from "@/components/app/terms-gate";

const agreeButton = () => screen.getByRole("button", { name: "Agree and continue" });
const box = () => screen.getByRole("checkbox", { name: /I have read and agree to the Terms of Service/ });

beforeEach(() => {
  vi.clearAllMocks();
  acceptTerms.mockResolvedValue(undefined);
});

describe("TermsGate", () => {
  it("links to the terms and the privacy policy in a new tab", () => {
    render(<TermsGate />);
    const terms = screen.getByRole("link", { name: "Terms of Service" });
    expect(terms).toHaveAttribute("href", "/terms");
    expect(terms).toHaveAttribute("target", "_blank");
    expect(screen.getByRole("link", { name: "Privacy Policy" })).toHaveAttribute("href", "/privacy");
  });

  it("says that a lead is no permission to contact anyone", () => {
    render(<TermsGate />);
    expect(screen.getByText(/It gives you no permission to call, text or email anyone/)).toBeInTheDocument();
  });

  it("starts with the box empty and the button off", () => {
    render(<TermsGate />);
    expect(box()).not.toBeChecked();
    expect(agreeButton()).toBeDisabled();
  });

  it("records nothing while the box is empty", () => {
    render(<TermsGate />);
    fireEvent.click(agreeButton());
    expect(acceptTerms).not.toHaveBeenCalled();
  });

  it("records the agreement to the version shown, then reloads the app", async () => {
    render(<TermsGate />);
    expect(box()).toHaveAccessibleName(new RegExp(`version ${TERMS_VERSION}`));
    fireEvent.click(box());
    fireEvent.click(agreeButton());
    await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    expect(acceptTerms).toHaveBeenCalledExactlyOnceWith(TERMS_VERSION, "token-1");
  });

  it("turns the button off again when the box is cleared", () => {
    render(<TermsGate />);
    fireEvent.click(box());
    expect(agreeButton()).toBeEnabled();
    fireEvent.click(box());
    expect(agreeButton()).toBeDisabled();
  });

  it("asks for a reload when the terms changed while the screen was open", async () => {
    acceptTerms.mockRejectedValue(new ApiError("terms_version_not_current", 409));
    render(<TermsGate />);
    fireEvent.click(box());
    fireEvent.click(agreeButton());
    expect(await screen.findByRole("alert")).toHaveTextContent(/The terms have just changed/);
    expect(refresh).not.toHaveBeenCalled();
  });

  it("says so and lets the user try again when the agreement is not recorded", async () => {
    acceptTerms.mockRejectedValue(new ApiError("boom", 500));
    render(<TermsGate />);
    fireEvent.click(box());
    fireEvent.click(agreeButton());
    expect(await screen.findByRole("alert")).toHaveTextContent(/could not record your agreement/);
    expect(refresh).not.toHaveBeenCalled();
    expect(agreeButton()).toBeEnabled();
  });

  it("lets a user who does not agree sign out", async () => {
    render(<TermsGate />);
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    await waitFor(() => expect(signOutAndRedirect).toHaveBeenCalledOnce());
    expect(acceptTerms).not.toHaveBeenCalled();
  });
});
