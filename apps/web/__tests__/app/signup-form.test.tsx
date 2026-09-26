// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
vi.mock("firebase/auth", () => ({
  signOut: vi.fn().mockResolvedValue(undefined),
  createUserWithEmailAndPassword: vi.fn(),
  signInWithPopup: vi.fn(),
  GoogleAuthProvider: vi.fn(),
}));

import { createUserWithEmailAndPassword, signInWithPopup, signOut } from "firebase/auth";
import { SignupForm } from "@/components/app/auth/signup-form";

const type = (label: string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
});

describe("SignupForm", () => {
  it("creates the account, sets the session cookie, and redirects to /app/leads", async () => {
    vi.mocked(createUserWithEmailAndPassword).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("id-token-456") },
    } as never);

    render(<SignupForm />);
    type("Email", "new@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/app/leads"));
    expect(createUserWithEmailAndPassword).toHaveBeenCalledWith({}, "new@example.com", "hunter2!!");
    expect(fetch).toHaveBeenCalledWith("/api/login", {
      method: "POST",
      headers: { Authorization: "Bearer id-token-456" },
    });
  });

  it("signs out of Firebase and shows an error when the session cookie request fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    vi.mocked(createUserWithEmailAndPassword).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("id-token-456") },
    } as never);
    render(<SignupForm />);
    type("Email", "new@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(/couldn.t start your session/);
    expect(signOut).toHaveBeenCalledWith({});
    expect(push).not.toHaveBeenCalled();
  });

  it("does not sign out when the session starts normally", async () => {
    vi.mocked(createUserWithEmailAndPassword).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("ok") },
    } as never);
    render(<SignupForm />);
    type("Email", "new@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));
    await waitFor(() => expect(push).toHaveBeenCalledWith("/app/leads"));
    expect(signOut).not.toHaveBeenCalled();
  });

  it("shows an inline error when the email is already registered", async () => {
    vi.mocked(createUserWithEmailAndPassword).mockRejectedValue({ code: "auth/email-already-in-use" });

    render(<SignupForm />);
    type("Email", "new@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText("An account with this email already exists.")).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it("requires a password of at least 8 characters before calling Firebase", async () => {
    render(<SignupForm />);
    type("Email", "new@example.com");
    type("Password", "short");
    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText("Use at least 8 characters.")).toBeInTheDocument();
    expect(createUserWithEmailAndPassword).not.toHaveBeenCalled();
  });

  it("toggles password visibility", () => {
    render(<SignupForm />);
    const input = screen.getByLabelText("Password");
    expect(input).toHaveAttribute("type", "password");
    fireEvent.click(screen.getByRole("button", { name: "Show password" }));
    expect(input).toHaveAttribute("type", "text");
  });

  it("creates the account with Google", async () => {
    vi.mocked(signInWithPopup).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("g") },
    } as never);
    render(<SignupForm />);
    fireEvent.click(screen.getByRole("button", { name: "Continue with Google" }));
    await waitFor(() => expect(push).toHaveBeenCalledWith("/app/leads"));
  });

  describe("?plan= pass-through from /pricing", () => {
    const signUpWith = async (search: string) => {
      window.history.replaceState(null, "", `/signup${search}`);
      vi.mocked(createUserWithEmailAndPassword).mockResolvedValue({
        user: { getIdToken: vi.fn().mockResolvedValue("t") },
      } as never);
      render(<SignupForm />);
      type("Email", "new@example.com");
      type("Password", "hunter2!!");
      fireEvent.click(screen.getByRole("button", { name: "Create account" }));
      await waitFor(() => expect(push).toHaveBeenCalled());
      window.history.replaceState(null, "", "/signup");
      return push.mock.calls[0][0] as string;
    };

    it("sends a valid plan to the account page preselected", async () => {
      expect(await signUpWith("?plan=TERRITORY")).toBe("/app/account?plan=TERRITORY");
    });

    it("normalizes the plan's case", async () => {
      expect(await signUpWith("?plan=starter")).toBe("/app/account?plan=STARTER");
    });

    it("ignores a plan outside the PlanTier union", async () => {
      expect(await signUpWith("?plan=ENTERPRISE")).toBe("/app/leads");
    });

    it("ignores an injected plan value", async () => {
      expect(await signUpWith("?plan=%2F%2Fevil.example")).toBe("/app/leads");
    });
  });
});
