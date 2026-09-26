"use client";

import { useId, useState } from "react";
import type { FormEvent } from "react";
import Link from "next/link";
import type { Market } from "@permittorch/types";
import { submitSampleLeadRequest } from "@/lib/api";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { SAMPLE_LEADS_PROMISE } from "@/components/marketing/sample-leads-copy";

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

type Status = "idle" | "submitting" | "success" | "error";
interface Errors { name?: string; email?: string; company?: string; marketSlug?: string }

export function SampleLeadsForm({ markets }: { markets: Market[] }) {
  const uid = useId();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [company, setCompany] = useState("");
  const [marketSlug, setMarketSlug] = useState("");
  const [errors, setErrors] = useState<Errors>({});
  const [status, setStatus] = useState<Status>("idle");

  function validate(): Errors {
    const e: Errors = {};
    if (!name.trim()) e.name = "Enter your name.";
    if (!EMAIL_RE.test(email.trim())) e.email = "Enter a valid work email.";
    if (!company.trim()) e.company = "Enter your company name.";
    if (!marketSlug) e.marketSlug = "Pick a market.";
    return e;
  }

  async function handleSubmit(ev: FormEvent) {
    ev.preventDefault();
    const e = validate();
    setErrors(e);
    if (Object.keys(e).length > 0) return;
    setStatus("submitting");
    try {
      await submitSampleLeadRequest({
        name: name.trim(), email: email.trim(), company: company.trim(), marketSlug,
      });
      setStatus("success");
    } catch {
      setStatus("error");
    }
  }

  if (status === "success") {
    return (
      <div className="rounded-xl border border-orange-200 bg-white p-8 text-center shadow-sm">
        <p className="text-lg font-semibold">Request received — check your inbox.</p>
        <p className="mt-2 text-neutral-600">We&apos;ll email you {SAMPLE_LEADS_PROMISE}.</p>
        <p className="mt-4 text-neutral-600">Want new opportunities every morning?</p>
        <Link href="/signup" className={cn(buttonVariants(), "mt-4 bg-orange-500 text-white hover:bg-orange-600")}>
          Start Free
        </Link>
      </div>
    );
  }

  const field = (
    id: string, label: string, value: string, set: (v: string) => void,
    error: string | undefined, type = "text", placeholder = "",
  ) => (
    <div>
      <label htmlFor={id} className="mb-1.5 block text-sm font-medium text-neutral-700">{label}</label>
      <Input id={id} type={type} value={value} placeholder={placeholder}
        onChange={(ev) => set(ev.target.value)} aria-invalid={Boolean(error)} />
      {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
    </div>
  );

  return (
    <form onSubmit={handleSubmit} noValidate
      className="grid gap-4 rounded-xl border border-neutral-200 bg-white p-6 shadow-sm sm:grid-cols-2">
      {field(`${uid}-name`, "Name", name, setName, errors.name, "text", "Dana Reyes")}
      {field(`${uid}-email`, "Work email", email, setEmail, errors.email, "email", "you@yourcompany.com")}
      {field(`${uid}-company`, "Company", company, setCompany, errors.company, "text", "Reyes Fire Protection")}
      <div>
        <label htmlFor={`${uid}-market`} className="mb-1.5 block text-sm font-medium text-neutral-700">
          Market
        </label>
        <select id={`${uid}-market`} value={marketSlug}
          onChange={(ev) => setMarketSlug(ev.target.value)}
          aria-invalid={Boolean(errors.marketSlug)}
          className="h-9 w-full rounded-md border border-neutral-200 bg-white px-3 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-orange-500">
          <option value="">Choose your market…</option>
          {markets.map((m) => (
            <option key={m.slug} value={m.slug}>{`${m.city}, ${m.state}`}</option>
          ))}
        </select>
        {errors.marketSlug && <p className="mt-1 text-sm text-red-600">{errors.marketSlug}</p>}
      </div>
      <div className="sm:col-span-2">
        <Button type="submit" disabled={status === "submitting"}
          className="w-full bg-orange-500 text-white hover:bg-orange-600">
          {status === "submitting" ? "Sending…" : "Send My Sample Leads"}
        </Button>
        {status === "error" && (
          <p className="mt-2 text-sm text-red-600">
            Something went wrong sending your request. Try again in a minute.
          </p>
        )}
        <p className="mt-2 text-center text-xs text-neutral-400">
          We&apos;ll email you {SAMPLE_LEADS_PROMISE}. No spam, no obligation.
        </p>
      </div>
    </form>
  );
}
