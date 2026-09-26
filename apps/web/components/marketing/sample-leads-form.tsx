"use client";

import { useEffect, useId, useRef, useState } from "react";
import type { FormEvent } from "react";
import Link from "next/link";
import type { Market } from "@permittorch/types";
import { ApiError, submitSampleLeadRequest } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ctaClasses } from "@/components/marketing/cta";
import { SAMPLE_LEADS_PROMISE } from "@/components/marketing/sample-leads-copy";

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export const RATE_LIMIT_MESSAGE = "Too many requests — please wait a minute and try again.";
export const GENERIC_ERROR_MESSAGE = "Something went wrong sending your request. Try again in a minute.";
export const INVALID_REQUEST_MESSAGE = "We couldn't accept that request. Check your details and try again.";
export const MARKETS_UPDATING_MESSAGE = "Markets are updating — check back shortly.";

// lib/api.ts falls back to this message when the API sent no { error } body.
const API_FALLBACK_MESSAGE = /^API request failed with status \d+$/;

type Status = "idle" | "submitting" | "success" | "error";
type FieldKey = "name" | "email" | "company" | "marketSlug";
type Errors = Partial<Record<FieldKey, string>>;
const FIELD_ORDER: FieldKey[] = ["name", "email", "company", "marketSlug"];

/** Map a failed request to a message the visitor can act on. */
export function sampleLeadErrorMessage(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.status === 429) return RATE_LIMIT_MESSAGE;
    if (err.status === 400) {
      return err.message && !API_FALLBACK_MESSAGE.test(err.message) ? err.message : INVALID_REQUEST_MESSAGE;
    }
  }
  return GENERIC_ERROR_MESSAGE;
}

export function SampleLeadsForm({ markets }: { markets: Market[] }) {
  const uid = useId();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [company, setCompany] = useState("");
  const [marketSlug, setMarketSlug] = useState("");
  const [errors, setErrors] = useState<Errors>({});
  const [status, setStatus] = useState<Status>("idle");
  const [submitError, setSubmitError] = useState("");
  // State updates are async; the ref blocks a second submit fired before re-render.
  const inFlight = useRef(false);
  const noMarkets = markets.length === 0;
  const successRef = useRef<HTMLHeadingElement>(null);
  const nameRef = useRef<HTMLInputElement>(null);
  const emailRef = useRef<HTMLInputElement>(null);
  const companyRef = useRef<HTMLInputElement>(null);
  const marketRef = useRef<HTMLSelectElement>(null);
  const inputRefs = { name: nameRef, email: emailRef, company: companyRef };
  const refs = { ...inputRefs, marketSlug: marketRef };

  useEffect(() => {
    if (status === "success") successRef.current?.focus();
  }, [status]);

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
    if (noMarkets || status === "submitting" || inFlight.current) return;
    const e = validate();
    setErrors(e);
    const firstInvalid = FIELD_ORDER.find((k) => e[k]);
    if (firstInvalid) {
      refs[firstInvalid].current?.focus();
      return;
    }
    inFlight.current = true;
    setSubmitError("");
    setStatus("submitting");
    try {
      await submitSampleLeadRequest({
        name: name.trim(), email: email.trim(), company: company.trim(), marketSlug,
      });
      setStatus("success");
    } catch (err) {
      setSubmitError(sampleLeadErrorMessage(err));
      setStatus("error");
    } finally {
      inFlight.current = false;
    }
  }

  if (status === "success") {
    return (
      <div className="rounded-xl border border-orange-200 bg-white p-8 text-center shadow-sm">
        <h3 ref={successRef} tabIndex={-1} className="text-lg font-semibold focus:outline-none">
          Request received — check your inbox.
        </h3>
        <p className="mt-2 text-neutral-600">We&apos;ll email you {SAMPLE_LEADS_PROMISE}.</p>
        <p className="mt-4 text-neutral-600">Want new opportunities every morning?</p>
        <Link href="/signup" className={ctaClasses("default", "mt-4")}>
          Start Free
        </Link>
      </div>
    );
  }

  const errorId = (k: FieldKey) => `${uid}-${k}-error`;
  const errorText = (k: FieldKey) =>
    errors[k] ? <p id={errorId(k)} className="mt-1 text-sm text-red-700">{errors[k]}</p> : null;

  const field = (
    key: Exclude<FieldKey, "marketSlug">, label: string, value: string, set: (v: string) => void,
    type = "text", placeholder = "",
  ) => (
    <div>
      <label htmlFor={`${uid}-${key}`} className="mb-1.5 block text-sm font-medium text-neutral-700">{label}</label>
      <Input id={`${uid}-${key}`} ref={inputRefs[key]} type={type} value={value}
        placeholder={placeholder} onChange={(ev) => set(ev.target.value)}
        aria-invalid={Boolean(errors[key])} aria-describedby={errors[key] ? errorId(key) : undefined} />
      {errorText(key)}
    </div>
  );

  return (
    <form onSubmit={handleSubmit} noValidate aria-busy={status === "submitting"}
      className="grid gap-4 rounded-xl border border-neutral-200 bg-white p-6 shadow-sm sm:grid-cols-2">
      {field("name", "Name", name, setName, "text", "Dana Reyes")}
      {field("email", "Work email", email, setEmail, "email", "you@yourcompany.com")}
      {field("company", "Company", company, setCompany, "text", "Reyes Fire Protection")}
      <div>
        <label htmlFor={`${uid}-marketSlug`} className="mb-1.5 block text-sm font-medium text-neutral-700">
          Market
        </label>
        <select id={`${uid}-marketSlug`} ref={marketRef} value={marketSlug}
          onChange={(ev) => setMarketSlug(ev.target.value)}
          aria-invalid={Boolean(errors.marketSlug)}
          disabled={noMarkets}
          aria-describedby={noMarkets ? `${uid}-markets-updating` : errors.marketSlug ? errorId("marketSlug") : undefined}
          className="h-9 w-full rounded-md border border-neutral-200 bg-white px-3 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-orange-700">
          <option value="">{noMarkets ? MARKETS_UPDATING_MESSAGE : "Choose your market…"}</option>
          {markets.map((m) => (
            <option key={m.slug} value={m.slug}>{`${m.city}, ${m.state}`}</option>
          ))}
        </select>
        {noMarkets && (
          <p id={`${uid}-markets-updating`} className="mt-1 text-sm text-neutral-600">
            {MARKETS_UPDATING_MESSAGE}
          </p>
        )}
        {errorText("marketSlug")}
      </div>
      <div className="sm:col-span-2">
        <Button type="submit" disabled={noMarkets || status === "submitting"}
          aria-describedby={noMarkets ? `${uid}-markets-updating` : undefined}
          className={ctaClasses("default", "w-full")}>
          {status === "submitting" ? "Sending…" : "Send My Sample Leads"}
        </Button>
        <div role="alert" className="empty:hidden">
          {status === "error" && submitError && (
            <p className="mt-2 text-sm text-red-700">{submitError}</p>
          )}
        </div>
        <p className="mt-2 text-center text-xs text-neutral-500">
          We&apos;ll email you {SAMPLE_LEADS_PROMISE}. No spam, no obligation.
        </p>
      </div>
    </form>
  );
}
