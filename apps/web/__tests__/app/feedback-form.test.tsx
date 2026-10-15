// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

vi.mock("@/lib/api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api")>()), submitFeedback: vi.fn() }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "tok" }));
vi.mock("next/navigation", () => ({ usePathname: () => "/app/leads" }));

import { ApiError, submitFeedback } from "@/lib/api";
import { FeedbackForm } from "@/components/feedback/feedback-form";
import { FeedbackDialog } from "@/components/feedback/feedback-dialog";

beforeEach(() => vi.clearAllMocks());

const send = () => screen.getByRole("button", { name: "Send" });
const typeMessage = (text: string) =>
  fireEvent.change(screen.getByRole("textbox", { name: /Message/ }), { target: { value: text } });

function fillAndSend(type = "Data request", message = "Add Austin") {
  fireEvent.click(screen.getByRole("radio", { name: type }));
  typeMessage(message);
  fireEvent.click(send());
}

describe("FeedbackForm", () => {
  it("shows the four types and a character counter", () => {
    render(<FeedbackForm pagePath="/app/leads" />);
    for (const label of ["Idea", "Problem", "Data request", "Other"])
      expect(screen.getByRole("radio", { name: label })).toBeInTheDocument();
    expect(screen.getByText("0 / 5000")).toBeInTheDocument();
    typeMessage("Hello");
    expect(screen.getByText("5 / 5000")).toBeInTheDocument();
  });

  it("keeps Send disabled until a type and a non-blank message are given", () => {
    render(<FeedbackForm pagePath="/app/leads" />);
    expect(send()).toBeDisabled();
    fireEvent.click(screen.getByRole("radio", { name: "Idea" }));
    expect(send()).toBeDisabled();
    typeMessage("   ");
    expect(send()).toBeDisabled();
    typeMessage("Real feedback");
    expect(send()).toBeEnabled();
  });

  it("sends the type, message and page, then thanks the user", async () => {
    vi.mocked(submitFeedback).mockResolvedValue(undefined);
    render(<FeedbackForm pagePath="/app/leads" />);

    fillAndSend();

    await waitFor(() => expect(submitFeedback).toHaveBeenCalledWith(
      { type: "DATA_REQUEST", message: "Add Austin", pagePath: "/app/leads" }, "tok"));
    expect(await screen.findByText("Thanks — we read every message.")).toBeInTheDocument();
  });

  it("disables Send while sending so a double click sends once", async () => {
    let resolve!: () => void;
    vi.mocked(submitFeedback).mockReturnValue(new Promise<void>((r) => { resolve = r; }));
    render(<FeedbackForm pagePath="/app/leads" />);

    fillAndSend();
    await waitFor(() => expect(send()).toBeDisabled());
    fireEvent.click(send());
    resolve();

    await screen.findByText("Thanks — we read every message.");
    expect(submitFeedback).toHaveBeenCalledTimes(1);
  });

  it("explains a rate limit", async () => {
    vi.mocked(submitFeedback).mockRejectedValue(new ApiError("Too many requests", 429));
    render(<FeedbackForm pagePath="/app/leads" />);
    fillAndSend();
    expect(await screen.findByText("You've sent a few already — try again in a few minutes.")).toBeInTheDocument();
  });

  it("shows the API's message on a 400", async () => {
    vi.mocked(submitFeedback).mockRejectedValue(new ApiError("message is required (max 5000 characters)", 400));
    render(<FeedbackForm pagePath="/app/leads" />);
    fillAndSend();
    expect(await screen.findByText("message is required (max 5000 characters)")).toBeInTheDocument();
  });

  it("keeps the typed text after any other error", async () => {
    vi.mocked(submitFeedback).mockRejectedValue(new Error("network"));
    render(<FeedbackForm pagePath="/app/leads" />);
    fillAndSend("Problem", "It broke");
    expect(await screen.findByText("Couldn't send — try again.")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: /Message/ })).toHaveValue("It broke");
  });
});

describe("FeedbackDialog", () => {
  it("sends the current page as the page path", async () => {
    vi.mocked(submitFeedback).mockResolvedValue(undefined);
    render(<FeedbackDialog open onOpenChange={() => {}} />);
    expect(screen.getByRole("dialog", { name: "Send feedback" })).toBeInTheDocument();

    fillAndSend("Idea", "Nice");

    await waitFor(() => expect(submitFeedback).toHaveBeenCalledWith(
      { type: "IDEA", message: "Nice", pagePath: "/app/leads" }, "tok"));
  });
});
