// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

// @testing-library/user-event is not installed (package.json is frozen), so
// these tests drive the form with fireEvent.
const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));
vi.mock("@/lib/firebase/client", () => ({ firebaseAuth: {} }));
vi.mock("firebase/auth", () => ({
  signOut: vi.fn().mockResolvedValue(undefined),
  signInWithEmailAndPassword: vi.fn(),
  signInWithPopup: vi.fn(),
  GoogleAuthProvider: vi.fn(),
  sendPasswordResetEmail: vi.fn(),
}));

import { sendPasswordResetEmail, signInWithEmailAndPassword, signInWithPopup, signOut } from "firebase/auth";
import { LoginForm } from "@/components/app/auth/login-form";

const type = (label: string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true }));
  window.history.replaceState(null, "", "/login");
});

describe("LoginForm", () => {
  it("signs in with email/password, sets the session cookie, and redirects to /app/leads", async () => {
    vi.mocked(signInWithEmailAndPassword).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("id-token-123") },
    } as never);

    render(<LoginForm />);
    type("Email", "rep@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/app/leads"));
    expect(signInWithEmailAndPassword).toHaveBeenCalledWith({}, "rep@example.com", "hunter2!!");
    expect(fetch).toHaveBeenCalledWith("/api/login", {
      method: "POST",
      headers: { Authorization: "Bearer id-token-123" },
    });
  });

  it("returns to the protected page the middleware redirected from", async () => {
    window.history.replaceState(null, "", "/login?redirect=%2Fapp%2Fsaved");
    vi.mocked(signInWithEmailAndPassword).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("t") },
    } as never);
    render(<LoginForm />);
    type("Email", "rep@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    await waitFor(() => expect(push).toHaveBeenCalledWith("/app/saved"));
  });

  it("ignores off-site redirect targets", async () => {
    window.history.replaceState(null, "", "/login?redirect=https%3A%2F%2Fevil.example");
    vi.mocked(signInWithEmailAndPassword).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("t") },
    } as never);
    render(<LoginForm />);
    type("Email", "rep@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    await waitFor(() => expect(push).toHaveBeenCalledWith("/app/leads"));
  });

  it("shows an inline error for invalid credentials instead of redirecting", async () => {
    vi.mocked(signInWithEmailAndPassword).mockRejectedValue({ code: "auth/invalid-credential" });

    render(<LoginForm />);
    type("Email", "rep@example.com");
    type("Password", "wrong");
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Incorrect email or password.")).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it("validates fields inline before calling Firebase", async () => {
    render(<LoginForm />);
    type("Email", "not-an-email");
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
    expect(screen.getByText("Enter your password.")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toHaveAttribute("aria-invalid", "true");
    expect(signInWithEmailAndPassword).not.toHaveBeenCalled();
  });

  it("does not start a session when the session cookie request fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    vi.mocked(signInWithEmailAndPassword).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("t") },
    } as never);
    render(<LoginForm />);
    type("Email", "rep@example.com");
    type("Password", "hunter2!!");
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(/couldn.t start your session/);
    expect(push).not.toHaveBeenCalled();
    // No half-signed-in state: the Firebase client session is dropped too.
    expect(signOut).toHaveBeenCalledWith({});
  });

  it("signs out of Firebase when the Google session cookie request fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
    vi.mocked(signInWithPopup).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("g") },
    } as never);
    render(<LoginForm />);
    fireEvent.click(screen.getByRole("button", { name: "Continue with Google" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(/couldn.t start your session/);
    expect(signOut).toHaveBeenCalledTimes(1);
    expect(push).not.toHaveBeenCalled();
  });

  it("signs in with Google", async () => {
    vi.mocked(signInWithPopup).mockResolvedValue({
      user: { getIdToken: vi.fn().mockResolvedValue("google-token") },
    } as never);
    render(<LoginForm />);
    fireEvent.click(screen.getByRole("button", { name: "Continue with Google" }));
    await waitFor(() => expect(push).toHaveBeenCalledWith("/app/leads"));
    expect(fetch).toHaveBeenCalledWith("/api/login", {
      method: "POST",
      headers: { Authorization: "Bearer google-token" },
    });
  });

  it("stays silent when the Google popup is closed by the user", async () => {
    vi.mocked(signInWithPopup).mockRejectedValue({ code: "auth/popup-closed-by-user" });
    render(<LoginForm />);
    fireEvent.click(screen.getByRole("button", { name: "Continue with Google" }));
    await waitFor(() => expect(signInWithPopup).toHaveBeenCalled());
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it("sends a password reset email for the entered address", async () => {
    vi.mocked(sendPasswordResetEmail).mockResolvedValue(undefined);
    render(<LoginForm />);
    type("Email", "rep@example.com");
    fireEvent.click(screen.getByRole("button", { name: "Forgot password?" }));

    expect(await screen.findByRole("heading", { name: "Reset your password" })).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toHaveValue("rep@example.com");
    fireEvent.click(screen.getByRole("button", { name: "Send reset link" }));

    expect(await screen.findByText(/reset link is on its way/)).toBeInTheDocument();
    expect(sendPasswordResetEmail).toHaveBeenCalledWith({}, "rep@example.com");
    fireEvent.click(screen.getByRole("button", { name: "Back to sign in" }));
    expect(screen.getByRole("button", { name: "Sign in" })).toBeInTheDocument();
  });
});
