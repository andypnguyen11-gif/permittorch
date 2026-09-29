/** A paragraph, a bulleted list, or a paragraph set apart so that it cannot be missed. */
export type LegalBlock = string | { list: string[] } | { notice: string };
export interface LegalSection { heading: string; body: LegalBlock[] }

const blockKey = (block: LegalBlock) =>
  (typeof block === "string" ? block : "list" in block ? block.list[0] : block.notice).slice(0, 32);

function Block({ block }: { block: LegalBlock }) {
  if (typeof block === "string") return <p className="mt-3 leading-relaxed text-neutral-700">{block}</p>;
  if ("notice" in block) {
    return <p className="mt-3 text-sm leading-relaxed font-semibold text-neutral-900">{block.notice}</p>;
  }
  return (
    <ul className="mt-3 list-disc space-y-2 pl-6 leading-relaxed text-neutral-700">
      {block.list.map((item) => <li key={item.slice(0, 32)}>{item}</li>)}
    </ul>
  );
}

export function LegalDocument({ title, updated, version, sections }: {
  title: string; updated: string; version: string; sections: LegalSection[];
}) {
  return (
    <article className="mx-auto max-w-3xl px-4 py-16 sm:px-6">
      <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
      <p className="mt-2 text-sm text-neutral-500">Last updated {updated}. Version {version}.</p>
      {sections.map((s) => (
        <section key={s.heading} className="mt-8">
          <h2 className="text-xl font-semibold">{s.heading}</h2>
          {s.body.map((block) => <Block key={blockKey(block)} block={block} />)}
        </section>
      ))}
    </article>
  );
}
