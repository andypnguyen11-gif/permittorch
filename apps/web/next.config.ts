import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  transpilePackages: ["@permittorch/types"],
};

export default nextConfig;
