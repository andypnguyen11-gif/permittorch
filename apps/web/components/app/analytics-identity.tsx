"use client";
import { useEffect } from "react";
import { onAuthStateChanged } from "firebase/auth";
import { firebaseAuth } from "@/lib/firebase/client";
import { identifyUser, initAnalytics, resetAnalyticsUser } from "@/lib/analytics";

/**
 * Ties analytics events to the signed-in user by uid only (no email or name). Mounted in the dashboard
 * layout only, so marketing pages never load the Firebase Auth SDK for analytics.
 */
export function AnalyticsIdentity() {
  useEffect(() => {
    // Child effects run before the root provider's effect: make sure PostHog is up first.
    initAnalytics();
    return onAuthStateChanged(firebaseAuth, (user) => {
      if (user) identifyUser(user.uid);
      else resetAnalyticsUser();
    });
  }, []);
  return null;
}
