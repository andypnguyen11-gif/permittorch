"use client";
import { usePathname } from "next/navigation";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { FeedbackForm } from "@/components/feedback/feedback-form";

/** The feedback form over the current page; the page's path (never its query string —
 *  usePathname carries none) goes along so the owner knows where it was sent from. */
export function FeedbackDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const pathname = usePathname();
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="w-full sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Send feedback</DialogTitle>
          <DialogDescription>Ideas, problems, or data you’d like to see — it all helps.</DialogDescription>
        </DialogHeader>
        <FeedbackForm pagePath={pathname} />
      </DialogContent>
    </Dialog>
  );
}
