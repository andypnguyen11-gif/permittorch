import {
  Accordion, AccordionContent, AccordionItem, AccordionTrigger,
} from "@/components/ui/accordion";

export interface FaqEntry { q: string; a: string }

/**
 * FAQ accordion whose answers are always present in the server HTML
 * (`hidden="until-found"`), so FAQPage JSON-LD describes content that is
 * actually on the page and browser find-in-page can reveal it.
 */
export function FaqAccordion({ items, className }: { items: FaqEntry[]; className?: string }) {
  return (
    <Accordion className={className} hiddenUntilFound>
      {items.map((f, i) => (
        <AccordionItem key={f.q} value={`faq-${i}`}>
          <AccordionTrigger className="text-left">{f.q}</AccordionTrigger>
          <AccordionContent className="text-neutral-600">{f.a}</AccordionContent>
        </AccordionItem>
      ))}
    </Accordion>
  );
}

/** schema.org FAQPage for the same entries rendered by FaqAccordion. */
export function faqPageJsonLd(items: FaqEntry[]) {
  return {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    mainEntity: items.map((f) => ({
      "@type": "Question",
      name: f.q,
      acceptedAnswer: { "@type": "Answer", text: f.a },
    })),
  };
}
