import { z } from "zod";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export const emailField = z
  .string()
  .trim()
  .min(1, "Enter your email.")
  .refine((v) => EMAIL_PATTERN.test(v), "Enter a valid email address.");

export const loginSchema = z.object({
  email: emailField,
  password: z.string().min(1, "Enter your password."),
});
export type LoginValues = z.infer<typeof loginSchema>;

export const signupSchema = z.object({
  email: emailField,
  password: z.string().min(8, "Use at least 8 characters."),
});
export type SignupValues = z.infer<typeof signupSchema>;

export const resetSchema = z.object({ email: emailField });
export type ResetValues = z.infer<typeof resetSchema>;
