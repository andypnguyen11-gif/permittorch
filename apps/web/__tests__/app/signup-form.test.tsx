// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
vi.mock("firebase/auth", () => ({
  createUserWithEmailAndPassword: vi.fn(),
  signInWithPopup: vi.fn(),
  GoogleAuthProvider: vi.fn(),
}));

import { createUserWithEmailAndPassword, signInWithPopup } from "firebase/auth";
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
});
