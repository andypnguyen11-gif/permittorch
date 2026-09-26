// Maps Firebase Auth error codes to plain-language, non-enumerating messages.
const MESSAGES: Record<string, string> = {
  "auth/invalid-credential": "Incorrect email or password.",
  "auth/invalid-login-credentials": "Incorrect email or password.",
  "auth/user-not-found": "Incorrect email or password.",
  "auth/wrong-password": "Incorrect email or password.",
  "auth/invalid-email": "Enter a valid email address.",
  "auth/missing-email": "Enter your email.",
  "auth/user-disabled": "This account has been disabled. Contact support for help.",
  "auth/too-many-requests": "Too many attempts. Try again in a few minutes.",
  "auth/network-request-failed": "Network error — check your connection and try again.",
  "auth/email-already-in-use": "An account with this email already exists.",
  "auth/weak-password": "Choose a stronger password (at least 8 characters).",
  "auth/popup-blocked": "Your browser blocked the Google sign-in window. Allow pop-ups and try again.",
  "auth/account-exists-with-different-credential":
    "An account already exists for this email. Sign in with your email and password.",
  "auth/operation-not-allowed": "This sign-in method isn’t enabled yet. Please contact support.",
  "session/failed": "You’re signed in, but we couldn’t start your session. Please try again.",
};

/** Errors caused by the user dismissing the Google popup — never shown. */
const SILENT = new Set([
  "auth/popup-closed-by-user",
  "auth/cancelled-popup-request",
  "auth/user-cancelled",
]);

export function authErrorCode(err: unknown): string {
  return typeof err === "object" && err !== null && "code" in err
    ? String((err as { code: unknown }).code)
    : "";
}

export function isSilentAuthError(err: unknown): boolean {
  return SILENT.has(authErrorCode(err));
}

export function authErrorMessage(err: unknown): string {
  return MESSAGES[authErrorCode(err)] ?? "Something went wrong. Please try again.";
}
