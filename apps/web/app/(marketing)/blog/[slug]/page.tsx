import Link from "next/link";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { buildMetadata, jsonLd } from "@/lib/seo";
import { articleJsonLd } from "@/lib/marketing/structured-data";
import { ctaClasses } from "@/components/marketing/cta";
import { blogPosts, getBlogPost } from "@/components/marketing/blog-posts";

export const dynamicParams = false;

interface Props { params: Promise<{ slug: string }> }

export function generateStaticParams() {
  return blogPosts.map((p) => ({ slug: p.slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const post = getBlogPost((await params).slug);
  if (!post) return {};
  return buildMetadata({
    title: post.title,
    description: post.description,
    path: `/blog/${post.slug}`,
  });
}

export default async function BlogPostPage({ params }: Props) {
  const post = getBlogPost((await params).slug);
  if (!post) notFound();

  return (
    <article className="mx-auto max-w-3xl px-4 py-20 sm:px-6">
      <Link href="/blog" className="text-sm text-neutral-500 hover:text-neutral-900">← All resources</Link>
      <h1 className="mt-4 text-4xl font-bold tracking-tight">{post.title}</h1>
      <p className="mt-3 text-sm text-neutral-500">
        {new Intl.DateTimeFormat("en-US", { year: "numeric", month: "long", day: "numeric", timeZone: "UTC" })
          .format(new Date(post.publishedAt))} · PermitTorch
      </p>
      <div className="mt-10 space-y-8">
        {post.sections.map((s, i) => (
          <section key={s.heading ?? `s-${i}`}>
            {s.heading && <h2 className="text-2xl font-semibold">{s.heading}</h2>}
            {s.body.map((p) => (
              <p key={p.slice(0, 32)} className="mt-4 leading-relaxed text-neutral-700">{p}</p>
            ))}
          </section>
        ))}
      </div>

      <aside className="mt-16 rounded-2xl bg-orange-50 p-8 text-center">
        <h2 className="text-xl font-bold">Get scored fire protection leads every morning</h2>
        <p className="mx-auto mt-2 max-w-md text-sm text-neutral-600">
          PermitTorch does everything in this guide automatically — monitored sources, fire-only
          classification, explainable 0–100 scores.
        </p>
        <Link href="/signup" className={ctaClasses("default", "mt-4")}>
          Start Free
        </Link>
      </aside>

      <script type="application/ld+json" dangerouslySetInnerHTML={jsonLd(articleJsonLd(post))} />
    </article>
  );
}
