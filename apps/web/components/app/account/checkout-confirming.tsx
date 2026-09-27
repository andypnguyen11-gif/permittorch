"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, RefreshCw } from "lucide-react";
import { getAccountMe } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { isSessionError, expireSession } from "@/components/app/sign-out";
import { Button } from "@/components/ui/button";
import { BillingButtons } from "./billing-buttons";

export const CONFIRM_POLL_MS = 2_000;
export const CONFIRM_TIMEOUT_MS = 30_000;

/**
 * Shown on /app/account?checkout=success until Stripe's webhook has attached the plan.
 * Polls /api/account/me; once a plan appears it swaps to the normal account view, and a live
 * subscription without a plan ends it with the billing-portal actions. It never
 * offers the market picker — a second checkout here is exactly the double-billing risk.
 */
export function CheckoutConfirming({ pollMs = CONFIRM_POLL_MS, timeoutMs = CONFIRM_TIMEOUT_MS }: {
  pollMs?: number; timeoutMs?: number;
} = {}) {
  const router = useRouter();
  const getToken = useApiToken();
  const [timedOut, setTimedOut] = useState(false);
  // A live Stripe subscription without a plan (e.g. `incomplete` after a failed first charge):
  // confirmation is over, and the fix is in the billing portal.
  const [needsAttention, setNeedsAttention] = useState(false);
  const [attempt, setAttempt] = useState(0);
  // Refs keep the polling loop tied to `attempt` alone: a re-render never restarts it.
  const deps = useRef({ router, getToken });
  deps.current = { router, getToken };

  const retry = useCallback(() => {
    setTimedOut(false);
    setAttempt((n) => n + 1);
  }, []);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const deadline = Date.now() + timeoutMs;

    const poll = async () => {
      try {
        const me = await getAccountMe(await deps.current.getToken());
        if (cancelled) return;
        if (me.plan !== null) {
          deps.current.router.replace("/app/account");
          deps.current.router.refresh();
          return;
        }
        if (me.hasLiveSubscription) {
          setNeedsAttention(true);
          return;
        }
      } catch (err) {
        if (cancelled) return;
        if (isSessionError(err)) { void expireSession(); return; }
        // Transient API errors: keep polling until the deadline.
      }
      if (Date.now() + pollMs > deadline) { setTimedOut(true); return; }
      timer = setTimeout(poll, pollMs);
    };
    timer = setTimeout(poll, 0);
    return () => { cancelled = true; if (timer) clearTimeout(timer); };
  }, [attempt, pollMs, timeoutMs]);

  if (needsAttention) {
    return (
      // BillingButtons(plan=null) carries the "needs attention" notice, worded as on the account page.
      <div role="status"><BillingButtons plan={null} /></div>
    );
  }
  if (timedOut) {
    return (
      <div role="status" className="space-y-3 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm">
        <p className="font-medium text-stone-900">We&apos;re still confirming your subscription — refresh in a minute.</p>
        <p className="text-stone-600">Your payment went to Stripe; your plan appears here as soon as Stripe confirms it. Please don&apos;t start another checkout.</p>
        <Button variant="outline" size="sm" onClick={retry}>
          <RefreshCw aria-hidden /> Check again
        </Button>
      </div>
    );
  }
  return (
    <div role="status" className="flex items-center gap-3 rounded-lg border border-border bg-stone-50 p-4 text-sm text-stone-700">
      <Loader2 className="size-4 animate-spin text-orange-500" aria-hidden />
      Confirming your subscription…
    </div>
  );
}
