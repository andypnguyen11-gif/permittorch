"use client";
import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Loader2, LogIn, RotateCw, ShieldAlert, TriangleAlert } from "lucide-react";
import { toast } from "sonner";
import { signOutAndRedirect } from "@/components/app/sign-out";
import { Button } from "@/components/ui/button";

/** Clears the session cookie, signs out of Firebase, and replaces the page with /login. */
export function SignInAgainButton({ variant = "default" }: { variant?: "default" | "outline" }) {
  const [pending, setPending] = useState(false);
  return (
    <Button
      variant={variant}
      disabled={pending}
      onClick={async () => {
        setPending(true);
        try {
          await signOutAndRedirect();
        } catch {
          toast.error("Could not sign out. Please try again.");
          setPending(false);
        }
      }}
    >
      {pending ? <Loader2 className="animate-spin" aria-hidden /> : <LogIn aria-hidden />}
      Sign in again
    </Button>
  );
}

/** Re-runs the server components for the current route. */
export function RetryButton({ onRetry }: { onRetry?: () => void }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  return (
    <Button
      disabled={pending}
      onClick={() => startTransition(() => {
        onRetry?.();
        router.refresh();
      })}
    >
      {pending ? <Loader2 className="animate-spin" aria-hidden /> : <RotateCw aria-hidden />}
      Try again
    </Button>
  );
}

function StateCard({ icon, title, children, actions }: {
  icon: React.ReactNode; title: string; children: React.ReactNode; actions: React.ReactNode;
}) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 rounded-xl border border-border bg-white px-6 py-16 text-center">
      {icon}
      <div>
        <p className="font-semibold text-stone-900">{title}</p>
        <p className="mx-auto mt-1 max-w-md text-sm text-stone-500">{children}</p>
      </div>
      <div className="mt-2 flex flex-wrap justify-center gap-2">{actions}</div>
    </div>
  );
}

export function SessionRecoveryCard() {
  return (
    <StateCard
      icon={
        <span className="flex size-11 items-center justify-center rounded-full bg-orange-50">
          <ShieldAlert className="size-5 text-orange-500" aria-hidden />
        </span>
      }
      title="We couldn’t verify your session"
      actions={<SignInAgainButton />}
    >
      Your sign-in may have expired or been revoked. Sign in again to get back to your leads.
    </StateCard>
  );
}

export function UnavailableCard({ onRetry }: { onRetry?: () => void }) {
  return (
    <StateCard
      icon={
        <span className="flex size-11 items-center justify-center rounded-full bg-red-50">
          <TriangleAlert className="size-5 text-red-500" aria-hidden />
        </span>
      }
      title="Something went wrong loading this page"
      actions={
        <>
          <RetryButton onRetry={onRetry} />
          <SignInAgainButton variant="outline" />
        </>
      }
    >
      The data service may be temporarily unavailable. Nothing you saved was lost.
    </StateCard>
  );
}
