import type { Metadata } from "next";
import { AuthShell } from "@/components/app/auth/auth-shell";
import { SignupForm } from "@/components/app/auth/signup-form";

// Root layout's title template appends "| PermitTorch".
export const metadata: Metadata = { title: "Sign up" };

export default function SignupPage() {
  return (
    <AuthShell>
      <SignupForm />
    </AuthShell>
  );
}
