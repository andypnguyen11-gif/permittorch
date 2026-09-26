import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: ["./vitest.setup.ts"],
    include: ["__tests__/**/*.test.{ts,tsx}"],
  },
  resolve: {
    alias: {
      // server-only throws when imported outside a React Server Components
      // environment; stub it out so lib/auth/config.ts can be unit tested.
      "server-only": fileURLToPath(new URL("./__tests__/stubs/server-only.ts", import.meta.url)),
      "@": import.meta.dirname,
    },
  },
});
