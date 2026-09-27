"use client";
import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { Loader2 } from "lucide-react";
import {
  GoogleAuthProvider, createUserWithEmailAndPassword, getAdditionalUserInfo, signInWithPopup,
} from "firebase/auth";
import { track } from "@/lib/analytics";
import { firebaseAuth } from "@/lib/firebase/client";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { authErrorMessage, isSilentAuthError } from "./firebase-errors";
import { Field, FormError, PasswordField } from "./field";
import { GoogleButton, OrDivider } from "./google-button";
import { signupSchema, type SignupValues } from "./schemas";
import { postSignupTarget, startSessionOrSignOut } from "./session";
import { zodResolver } from "./zod-resolver";

export function SignupForm() {
  const router = useRouter();
  const [formError, setFormError] = useState<string | null>(null);
  const [googlePending, setGooglePending] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<SignupValues>({
    resolver: zodResolver(signupSchema),
    mode: "onTouched",
    defaultValues: { email: "", password: "" },
  });

  const finish = async (idToken: string, isNewUser: boolean) => {
    await startSessionOrSignOut(idToken);
    // Google on /signup can sign an existing account in; only count real sign-ups.
    if (isNewUser) track("signup", {});
    setRedirecting(true);
    router.push(postSignupTarget());
  };

  const onSubmit = async ({ email, password }: SignupValues) => {
    setFormError(null);
    try {
      const credential = await createUserWithEmailAndPassword(firebaseAuth, email, password);
      await finish(await credential.user.getIdToken(), true);
    } catch (err) {
      setFormError(authErrorMessage(err));
    }
  };

  const onGoogle = async () => {
    setFormError(null);
    setGooglePending(true);
    try {
      const credential = await signInWithPopup(firebaseAuth, new GoogleAuthProvider());
      await finish(await credential.user.getIdToken(), getAdditionalUserInfo(credential)?.isNewUser ?? false);
    } catch (err) {
      if (!isSilentAuthError(err)) setFormError(authErrorMessage(err));
    } finally {
      setGooglePending(false);
    }
  };

  const busy = isSubmitting || googlePending || redirecting;

  return (
    <Card className="shadow-sm [--card-spacing:--spacing(6)]">
      <CardHeader>
        <CardTitle><h1 className="text-xl font-semibold tracking-tight">Create your PermitTorch account</h1></CardTitle>
        <CardDescription>Start finding fire-protection permits worth chasing.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <GoogleButton onClick={onGoogle} pending={googlePending} disabled={busy} />
        <OrDivider />
        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <Field id="email" label="Email" type="email" autoComplete="email" inputMode="email"
            error={errors.email?.message} {...register("email")} />
          <PasswordField id="password" label="Password" autoComplete="new-password"
            hint="At least 8 characters." error={errors.password?.message} {...register("password")} />
          <FormError message={formError} />
          <Button type="submit" size="lg" className="h-10 w-full" disabled={busy}>
            {(isSubmitting || redirecting) && <Loader2 className="animate-spin" aria-hidden />}
            Create account
          </Button>
          <p className="text-center text-xs text-stone-400">
            By creating an account you agree to our{" "}
            <Link href="/terms" className="underline hover:text-stone-600">Terms</Link> and{" "}
            <Link href="/privacy" className="underline hover:text-stone-600">Privacy Policy</Link>.
          </p>
        </form>
        <p className="text-center text-sm text-stone-500">
          Already have an account?{" "}
          <Link href="/login" className="font-medium text-orange-600 hover:underline">Sign in</Link>
        </p>
      </CardContent>
    </Card>
  );
}
