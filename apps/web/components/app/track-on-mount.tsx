"use client";
import { useEffect } from "react";
import { initAnalytics, track, type AnalyticsEvent, type EventProps } from "@/lib/analytics";

/** Fires one analytics event when a (server-rendered) page mounts, e.g. pricing_viewed or lead_opened. */
export function TrackOnMount<E extends AnalyticsEvent>({ event, props }: { event: E; props: EventProps[E] }) {
  const key = JSON.stringify(props);
  useEffect(() => {
    // Child effects run before the root provider's effect: make sure PostHog is up first.
    initAnalytics();
    track(event, JSON.parse(key) as EventProps[E]);
  }, [event, key]);
  return null;
}
