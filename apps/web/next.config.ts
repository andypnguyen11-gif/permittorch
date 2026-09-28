import path from "node:path";
import type { NextConfig } from "next";
import { assertApiUrlConfigured } from "./lib/api-base-url";
import { canonicalHostRedirects } from "./lib/canonical-host";

// Fail the production build (not the first browser request) when the API URL is missing.
assertApiUrlConfigured(process.env);

const nextConfig: NextConfig = {
  transpilePackages: ["@permittorch/types"],
  // Self-contained server bundle for the Docker image; the monorepo root as tracing root
  // puts the entry at .next/standalone/apps/web/server.js with workspace deps included.
  output: "standalone",
  outputFileTracingRoot: path.join(__dirname, "../../"),
  // www.permittorch.com -> permittorch.com. Evaluated before the middleware, for every path.
  redirects: async () => canonicalHostRedirects(),
};

export default nextConfig;
