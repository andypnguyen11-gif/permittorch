import { useId } from "react";
import { cn } from "@/lib/utils";

/** Card with a real heading, exposed to assistive tech as a named region. */
export function SectionCard({ title, action, children, className }: {
  title: React.ReactNode; action?: React.ReactNode; children: React.ReactNode; className?: string;
}) {
  const id = useId();
  return (
    <section aria-labelledby={id}
      className={cn("rounded-xl border border-border bg-white shadow-xs", className)}>
      <div className="flex items-center justify-between gap-3 px-5 pt-4 pb-3">
        <h2 id={id} className="text-sm font-semibold text-stone-900">{title}</h2>
        {action}
      </div>
      <div className="px-5 pb-5">{children}</div>
    </section>
  );
}
