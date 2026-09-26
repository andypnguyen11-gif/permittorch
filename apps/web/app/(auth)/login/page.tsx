import type { Metadata } from "next";
import { AuthShell } from "@/components/app/auth/auth-shell";
import { LoginForm } from "@/components/app/auth/login-form";

// Root layout's title template appends "| PermitTorch".
export const metadata: Metadata = { title: "Sign in" };

export default function LoginPage() {
  return (
    <AuthShell>
      <LoginForm />
    </AuthShell>
  );
}
