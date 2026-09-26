"use client";

import { useEffect, useState } from "react";
import { absoluteUpdatedLabel, relativeUpdatedLabel } from "@/lib/marketing/freshness";

const REFRESH_MS = 60_000;

/**
 * Renders the absolute timestamp on the server (and for no-JS clients), then
 * switches to a relative label computed from the viewer's clock on mount and
 * recomputes it every minute. The relative label is never baked into static HTML.
 */
export function FreshnessLabel({ lastUpdatedAt }: { lastUpdatedAt: string }) {
  const [label, setLabel] = useState(() => absoluteUpdatedLabel(lastUpdatedAt));

  useEffect(() => {
    const update = () => setLabel(relativeUpdatedLabel(lastUpdatedAt, new Date(Date.now())));
    update();
    const id = setInterval(update, REFRESH_MS);
    return () => clearInterval(id);
  }, [lastUpdatedAt]);

  return (
    <time dateTime={lastUpdatedAt} title={absoluteUpdatedLabel(lastUpdatedAt)}>
      {label}
    </time>
  );
}
