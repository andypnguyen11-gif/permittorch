"use client";
import { useState } from "react";
import type { FeedbackType } from "@permittorch/types";
import { ApiError, submitFeedback } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";

export const MAX_FEEDBACK_LENGTH = 5000;

const TYPES: Array<{ value: FeedbackType; label: string }> = [
  { value: "IDEA", label: "Idea" },
  { value: "PROBLEM", label: "Problem" },
  { value: "DATA_REQUEST", label: "Data request" },
  { value: "OTHER", label: "Other" },
];

function errorText(err: unknown): string {
  if (err instanceof ApiError && err.status === 429) return "You've sent a few already — try again in a few minutes.";
  if (err instanceof ApiError && err.status === 400) return err.message;
  return "Couldn't send — try again.";
}

/** Feedback from a signed-in user. Who sent it comes from their login on the API side,
 *  so the form asks only for a type and a message; `pagePath` is attached for context. */
export function FeedbackForm({ pagePath }: { pagePath: string | null }) {
  const getToken = useApiToken();
  const [type, setType] = useState<FeedbackType | null>(null);
  const [message, setMessage] = useState("");
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (sent) return <p className="py-6 text-center text-sm text-stone-700">Thanks — we read every message.</p>;

  const canSend = type !== null && message.trim().length > 0 && !sending;

  const onSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!canSend) return;
    setSending(true);
    setError(null);
    try {
      await submitFeedback({ type, message: message.trim(), pagePath }, await getToken());
      setSent(true);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setSending(false);
    }
  };

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      <fieldset>
        <legend className="mb-2 text-sm font-medium text-stone-900">What kind of feedback?</legend>
        <div className="grid grid-cols-2 gap-2">
          {TYPES.map((option) => (
            <label
              key={option.value}
              className={cn(
                "flex min-h-11 cursor-pointer items-center justify-center rounded-lg border px-3 text-sm font-medium transition-colors has-[:focus-visible]:ring-3 has-[:focus-visible]:ring-ring/50",
                type === option.value
                  ? "border-orange-500 bg-orange-50 text-orange-700"
                  : "border-border bg-white text-stone-700 hover:bg-stone-50",
              )}
            >
              <input
                type="radio"
                name="feedback-type"
                value={option.value}
                checked={type === option.value}
                onChange={() => setType(option.value)}
                className="sr-only"
              />
              {option.label}
            </label>
          ))}
        </div>
      </fieldset>
      <div className="space-y-2">
        <Label htmlFor="feedback-message">Message</Label>
        <Textarea
          id="feedback-message"
          rows={6}
          maxLength={MAX_FEEDBACK_LENGTH}
          value={message}
          onChange={(event) => setMessage(event.target.value)}
          placeholder="Tell us what would make PermitTorch more useful."
          className="w-full"
        />
        <p className="text-right text-xs text-stone-500">{message.length} / {MAX_FEEDBACK_LENGTH}</p>
      </div>
      {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
      <Button type="submit" disabled={!canSend} className="w-full">
        Send
      </Button>
    </form>
  );
}
