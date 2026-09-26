"use client";
import { useCallback } from "react";
import { firebaseAuth } from "@/lib/firebase/client";

export function useApiToken(): () => Promise<string> {
  return useCallback(async () => {
    if (process.env.NEXT_PUBLIC_API_MOCK === "1") return "mock-token";
    // firebaseAuth.currentUser is null until the client SDK's auth-state
    // listener has fired at least once — wait for it before reading currentUser.
    await firebaseAuth.authStateReady();
    return (await firebaseAuth.currentUser?.getIdToken()) ?? "";
  }, []);
}
