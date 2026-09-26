"use client";
import { useCallback } from "react";
import { firebaseAuth } from "@/lib/firebase/client";
import { SessionExpiredError, expireSession } from "@/components/app/sign-out";

/**
 * Returns a function that resolves a fresh Firebase ID token for API calls.
 * It never resolves an empty bearer: with no signed-in user it starts the
 * "session expired" sign-out flow and throws SessionExpiredError, which the
 * caller's catch hands to reportMutationError (a no-op second toast is avoided).
 */
export function useApiToken(): () => Promise<string> {
  return useCallback(async () => {
    if (process.env.NEXT_PUBLIC_API_MOCK === "1") return "mock-token";
    // firebaseAuth.currentUser is null until the client SDK's auth-state
    // listener has fired at least once — wait for it before reading currentUser.
    await firebaseAuth.authStateReady();
    const user = firebaseAuth.currentUser;
    if (!user) {
      void expireSession();
      throw new SessionExpiredError();
    }
    return user.getIdToken();
  }, []);
}
