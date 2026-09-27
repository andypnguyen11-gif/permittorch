"use client";
import { useEffect } from "react";
import { onAuthStateChanged } from "firebase/auth";
import { firebaseAuth } from "@/lib/firebase/client";
import { identifyUser, initAnalytics, resetAnalyticsUser } from "@/lib/analytics";

/**
 * Ties analytics events to the signed-in user by their internal PermitTorch user id
 * (`/api/account/me` → `id`) — never the Firebase uid, email or name. Mounted in the dashboard
 * layout only (which has already loaded the account), so marketing pages never load the
 * Firebase Auth SDK for analytics.
 */
export function AnalyticsIdentity({ userId }: { userId: string }) {
  useEffect(() => {
    // Child effects run before the root provider's effect: make sure PostHog is up first.
    initAnalytics();
    identifyUser(userId);
    return onAuthStateChanged(firebaseAuth, (user) => {
      if (!user) resetAnalyticsUser();
    });
  }, [userId]);
  return null;
}
