import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: "./src/test/setupTests.ts",
    css: true,

    /*
     * P01/P02 validation stability:
     * On Windows + Vitest 4, the default forks pool can timeout while starting
     * many workers in parallel. This is not a product-code failure; it is a
     * worker orchestration failure. Use one threads worker for deterministic CI/local proof.
     */
    pool: "threads",
    fileParallelism: false,
    maxWorkers: 1,
    minWorkers: 1,
    testTimeout: 30000,
    hookTimeout: 30000,
    teardownTimeout: 30000,

    include: [
      "src/**/*.test.ts",
      "src/**/*.test.tsx",
      "src/**/*.integration.test.ts",
      "src/**/*.integration.test.tsx"
    ],
    exclude: [
      "node_modules/**",
      "dist/**",
      "coverage/**",
      "playwright-report/**",
      "test-results/**",
      "e2e/**"
    ],
    coverage: {
      provider: "v8",
      reporter: ["text", "html", "lcov"],
      include: ["src/**/*.{ts,tsx}"],
      exclude: [
        "src/main.tsx",
        "src/test/**",
        "src/**/*.test.{ts,tsx}",
        "src/**/*.spec.{ts,tsx}",
        "src/**/*.d.ts"
      ]
    }
  }
});
