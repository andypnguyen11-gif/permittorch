"use client";
import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { CheckCircle2, Loader2 } from "lucide-react";
import {
  GoogleAuthProvider, sendPasswordResetEmail, signInWithEmailAndPassword, signInWithPopup,
} from "firebase/auth";
import { firebaseAuth } from "@/lib/firebase/client";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { authErrorMessage, isSilentAuthError } from "./firebase-errors";
import { Field, FormError, PasswordField } from "./field";
import { GoogleButton, OrDivider } from "./google-button";
import { loginSchema, resetSchema, type LoginValues, type ResetValues } from "./schemas";
import { postSignInTarget, startSession } from "./session";
import { zodResolver } from "./zod-resolver";

function ResetPasswordForm({ initialEmail, onBack }: { initialEmail: string; onBack: () => void }) {
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<ResetValues>({
    resolver: zodResolver(resetSchema),
    mode: "onTouched",
    defaultValues: { email: initialEmail },
  });

  const onSubmit = async ({ email }: ResetValues) => {
    setError(null);
    try {
      await sendPasswordResetEmail(firebaseAuth, email);
      setSentTo(email);
    } catch (err) {
      setError(authErrorMessage(err));
    }
  };

  return (
    <Card className="shadow-sm [--card-spacing:--spacing(6)]">
      <CardHeader>
        <CardTitle><h1 className="text-xl font-semibold tracking-tight">Reset your password</h1></CardTitle>
        <CardDescription>We’ll email you a link to choose a new password.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {sentTo ? (
          <p role="status" className="flex gap-2 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>If an account exists for {sentTo}, a reset link is on its way. Check your inbox.</span>
          </p>
        ) : (
          <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
            <Field id="reset-email" label="Email" type="email" autoComplete="email" autoFocus
              error={errors.email?.message} {...register("email")} />
            <FormError message={error} />
            <Button type="submit" size="lg" className="h-10 w-full" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="animate-spin" aria-hidden />}
              Send reset link
            </Button>
          </form>
        )}
        <Button type="button" variant="ghost" className="w-full" onClick={onBack}>
          Back to sign in
        </Button>
      </CardContent>
    </Card>
  );
}

export function LoginForm() {
  const router = useRouter();
  const [mode, setMode] = useState<"signin" | "reset">("signin");
  const [formError, setFormError] = useState<string | null>(null);
  const [googlePending, setGooglePending] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const { register, handleSubmit, getValues, formState: { errors, isSubmitting } } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    mode: "onTouched",
    defaultValues: { email: "", password: "" },
  });

  const finish = async (idToken: string) => {
    await startSession(idToken);
    setRedirecting(true);
    router.push(postSignInTarget());
  };

  const onSubmit = async ({ email, password }: LoginValues) => {
    setFormError(null);
    try {
      const credential = await signInWithEmailAndPassword(firebaseAuth, email, password);
      await finish(await credential.user.getIdToken());
    } catch (err) {
      setFormError(authErrorMessage(err));
    }
  };

  const onGoogle = async () => {
    setFormError(null);
    setGooglePending(true);
    try {
      const credential = await signInWithPopup(firebaseAuth, new GoogleAuthProvider());
      await finish(await credential.user.getIdToken());
    } catch (err) {
      if (!isSilentAuthError(err)) setFormError(authErrorMessage(err));
    } finally {
      setGooglePending(false);
    }
  };

  if (mode === "reset") {
    return <ResetPasswordForm initialEmail={getValues("email")} onBack={() => setMode("signin")} />;
  }

  const busy = isSubmitting || googlePending || redirecting;

  return (
    <Card className="shadow-sm [--card-spacing:--spacing(6)]">
      <CardHeader>
        <CardTitle><h1 className="text-xl font-semibold tracking-tight">Sign in to PermitTorch</h1></CardTitle>
        <CardDescription>Welcome back — your leads are waiting.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <GoogleButton onClick={onGoogle} pending={googlePending} disabled={busy} />
        <OrDivider />
        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <Field id="email" label="Email" type="email" autoComplete="email" inputMode="email"
            error={errors.email?.message} {...register("email")} />
          <PasswordField id="password" label="Password" autoComplete="current-password"
            error={errors.password?.message}
            action={
              <button type="button" onClick={() => setMode("reset")}
                className="rounded text-xs font-medium text-orange-600 outline-none hover:underline focus-visible:ring-3 focus-visible:ring-ring/50">
                Forgot password?
              </button>
            }
            {...register("password")} />
          <FormError message={formError} />
          <Button type="submit" size="lg" className="h-10 w-full" disabled={busy}>
            {(isSubmitting || redirecting) && <Loader2 className="animate-spin" aria-hidden />}
            Sign in
          </Button>
        </form>
        <p className="text-center text-sm text-stone-500">
          New to PermitTorch?{" "}
          <Link href="/signup" className="font-medium text-orange-600 hover:underline">Create an account</Link>
        </p>
      </CardContent>
    </Card>
  );
}
