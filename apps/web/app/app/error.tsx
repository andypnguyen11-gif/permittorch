"use client";
import { useEffect } from "react";
import { SESSION_EXPIRED_DIGEST } from "@/components/app/session-digest";
import { SessionRecoveryCard, UnavailableCard } from "@/components/app/session-recovery";

export default function AppError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  // 401s from server components carry SESSION_EXPIRED_DIGEST (see api-errors.ts).
  if (error.digest === SESSION_EXPIRED_DIGEST) return <SessionRecoveryCard />;
  return <UnavailableCard onRetry={reset} />;
}
