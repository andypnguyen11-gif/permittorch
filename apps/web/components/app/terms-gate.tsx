"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import { ApiError, acceptTerms } from "@/lib/api";
import { TERMS_VERSION } from "@/lib/terms";
import { isSessionError, reportMutationError, signOutAndRedirect } from "@/components/app/sign-out";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";

// A short reading of the terms' main duties. The terms themselves are what the user agrees to.
const MAIN_POINTS = [
  "A lead is a public record. It gives you no permission to call, text or email anyone.",
  "You are responsible for following the laws on sales calls, texts and email, including do-not-call lists.",
  "No robocalls, prerecorded or artificial voices, or automated texts to a number you got from PermitTorch.",
  "When a person asks you to stop contacting them, you stop.",
  "The data is not for decisions about anyone’s credit, insurance, employment or housing.",
];

const legalLink = "font-medium text-orange-700 underline hover:text-orange-800";

/**
 * Shown in place of the app until the user agrees to the current terms. The API refuses lead
 * data until then, so this screen is where the agreement is made, not what enforces it.
 */
export function TermsGate() {
  const router = useRouter();
  const getToken = useApiToken();
  const [agreed, setAgreed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onAgree = async () => {
    setError(null);
    setBusy(true);
    try {
      await acceptTerms(TERMS_VERSION, await getToken());
      router.refresh();
    } catch (err) {
      setBusy(false);
      if (isSessionError(err)) reportMutationError(err, "");
      else if (err instanceof ApiError && err.status === 409) {
        setError("The terms have just changed. Reload this page to read the current version.");
      } else setError("We could not record your agreement. Please try again.");
    }
  };

  const onSignOut = async () => {
    try {
      await signOutAndRedirect();
    } catch {
      toast.error("Could not sign out. Please try again.");
    }
  };

  return (
    <section aria-labelledby="terms-gate-title" className="rounded-xl border border-border bg-white p-6 sm:p-8">
      <h1 id="terms-gate-title" className="text-xl font-semibold tracking-tight text-stone-900">
        Agree to the terms to continue
      </h1>
      <p className="mt-2 text-sm text-stone-600">
        Leads can show the phone number or email address of a person named on a permit. Before you
        use them, please read our{" "}
        <a href="/terms" target="_blank" rel="noopener" className={legalLink}>Terms of Service</a> and{" "}
        <a href="/privacy" target="_blank" rel="noopener" className={legalLink}>Privacy Policy</a>.
      </p>
      <p className="mt-5 text-sm font-medium text-stone-900">The main points:</p>
      <ul className="mt-2 list-disc space-y-1.5 pl-5 text-sm text-stone-700">
        {MAIN_POINTS.map((point) => <li key={point}>{point}</li>)}
      </ul>
      <p className="mt-3 text-sm text-stone-600">
        This is a summary. The Terms of Service are what you agree to.
      </p>
      <div className="mt-6 flex items-start gap-3">
        <input id="terms-agree" type="checkbox" checked={agreed} disabled={busy}
          onChange={(e) => setAgreed(e.target.checked)}
          className="mt-0.5 size-4 shrink-0 accent-orange-600" />
        <label htmlFor="terms-agree" className="text-sm text-stone-900">
          I have read and agree to the Terms of Service and the Privacy Policy, version {TERMS_VERSION}.
        </label>
      </div>
      {error && (
        <p role="alert" className="mt-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}
      <div className="mt-6 flex flex-wrap gap-2">
        <Button size="lg" disabled={!agreed || busy} onClick={onAgree}>
          {busy && <Loader2 className="animate-spin" aria-hidden />}
          Agree and continue
        </Button>
        <Button size="lg" variant="ghost" disabled={busy} onClick={onSignOut}>
          Sign out
        </Button>
      </div>
    </section>
  );
}
