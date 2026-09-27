"use client";
import { useEffect } from "react";
import { initAnalytics } from "@/lib/analytics";

/** Root-layout mount point: initialises PostHog once per page load (no-op without a key). */
export function AnalyticsProvider({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    initAnalytics();
  }, []);
  return <>{children}</>;
}
