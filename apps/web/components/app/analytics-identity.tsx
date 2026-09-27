"use client";
import { useEffect } from "react";
import { onAuthStateChanged } from "firebase/auth";
import { firebaseAuth } from "@/lib/firebase/client";
import { identifyUser, initAnalytics, resetAnalyticsUser } from "@/lib/analytics";

/**
 * Ties analytics events to the signed-in Firebase user. Mounted in the dashboard
 * layout only, so marketing pages never load the Firebase Auth SDK for analytics.
 */
export function AnalyticsIdentity() {
  useEffect(() => {
    // Child effects run before the root provider's effect: make sure PostHog is up first.
    initAnalytics();
    return onAuthStateChanged(firebaseAuth, (user) => {
      if (user) identifyUser(user.uid, user.email ?? undefined);
      else resetAnalyticsUser();
    });
  }, []);
  return null;
}
