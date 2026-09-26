import { describe, expect, it } from "vitest";
import { blogPosts, getBlogPost } from "@/components/marketing/blog-posts";

describe("blogPosts", () => {
  it("contains the three launch posts with unique slugs", () => {
    expect(blogPosts.map((p) => p.slug).sort()).toEqual([
      "how-fire-sprinkler-contractors-find-leads",
      "how-to-find-commercial-fire-protection-projects",
      "using-building-permits-for-lead-generation",
    ]);
  });

  it("every post has title, description, and a valid ISO date", () => {
    for (const p of blogPosts) {
      expect(p.title.length).toBeGreaterThan(10);
      expect(p.description.length).toBeGreaterThan(40);
      expect(Number.isNaN(Date.parse(p.publishedAt))).toBe(false);
    }
  });

  it("every post body is at least 300 words", () => {
    for (const p of blogPosts) {
      const words = p.sections
        .flatMap((s) => s.body)
        .join(" ")
        .split(/\s+/)
        .filter(Boolean).length;
      expect(words, `${p.slug} has ${words} words`).toBeGreaterThanOrEqual(300);
    }
  });

  it("getBlogPost resolves known slugs and returns undefined otherwise", () => {
    expect(getBlogPost("using-building-permits-for-lead-generation")?.title).toBeTruthy();
    expect(getBlogPost("nope")).toBeUndefined();
  });
});
