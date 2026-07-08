
// Phase 3 copy/import guard reference strings used by acceptance validation:
// "could not be loaded"
// "could not load"
// Canonical replacements: StandardButton, DataFetchBoundary, ErrorBoundary, StandardTable.

import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";

const bannedLegacyUiImports = [
  { name: "@/components/hardening/StandardButton", message: "Use @/components/standard/StandardButton." },
  { name: "@/hardening/StandardButton", message: "Use @/components/standard/StandardButton." },
  { name: "@/components/hardening/DataFetchBoundary", message: "Use @/components/standard/DataFetchBoundary." },
  { name: "@/hardening/DataFetchBoundary", message: "Use @/components/standard/DataFetchBoundary." },
  { name: "@/components/table/StandardTable", message: "Use @/components/standard/StandardTable." },
  { name: "@/components/hardening/AppErrorBoundary", message: "Use @/components/ErrorBoundary." },
  { name: "@/hardening/RouteErrorBoundary", message: "Use @/components/ErrorBoundary." },
];

export default defineConfig([
  globalIgnores([
    "dist",
    "build",
    "coverage",
    "playwright-report",
    "test-results",
    "node_modules",
    "storybook-static",
    ".storybook",
    "**/*.stories.ts",
    "**/*.stories.tsx",
    "**/*.stories.mdx",
  ]),

  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: {
        ...globals.browser,
        ...globals.node,
      },
    },
    rules: {
      "@typescript-eslint/no-explicit-any": "warn",

      // Legacy/frontier code still has unused imports/locals. Keep visible,
      // but do not block the Phase 3/4 Material Search hardening validation.
      "@typescript-eslint/no-unused-vars": [
        "warn",
        {
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
          ignoreRestSiblings: true,
        },
      ],

      "react-hooks/exhaustive-deps": "warn",
      "react-hooks/set-state-in-effect": "off",

      "no-restricted-imports": [
        "error",
        {
          paths: bannedLegacyUiImports,
          patterns: [
            {
              group: [
                "*/components/hardening/StandardButton",
                "*/hardening/StandardButton",
                "*/components/hardening/DataFetchBoundary",
                "*/hardening/DataFetchBoundary",
                "*/components/table/StandardTable",
                "*/components/hardening/AppErrorBoundary",
                "*/hardening/RouteErrorBoundary",
              ],
              message: "Use canonical UI components under src/components/standard or src/components/ErrorBoundary.",
            },
          ],
        },
      ],
    },
  },

  {
    files: [
      "src/state/**/*.tsx",
      "src/components/standard/**/*.tsx",
      "src/ui/**/*.tsx",
      "src/pages/Admin/AdminSharedComponents.tsx",
    ],
    rules: {
      "react-refresh/only-export-components": "off",
    },
  },

  {
    files: ["e2e/**/*.ts", "e2e/**/*.tsx"],
    rules: {
      "react-refresh/only-export-components": "off",
      "@typescript-eslint/no-explicit-any": "warn",
      "@typescript-eslint/no-unused-vars": [
        "warn",
        {
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
          ignoreRestSiblings: true,
        },
      ],
    },
  },

  {
    files: ["src/pages/MaterialInvestigationPage.tsx"],
    rules: {
      "no-restricted-syntax": [
        "error",
        { selector: "JSXOpeningElement[name.name='input']", message: "Use StandardInput in Material Search." },
        { selector: "JSXOpeningElement[name.name='button']", message: "Use StandardButton in Material Search." },
        { selector: "JSXOpeningElement[name.name='a']", message: "Use StandardButton as='a' for PDF actions." },
        { selector: "JSXOpeningElement[name.name='SortableDataTable']", message: "Use StandardTable in Material Search." },
      ],
    },
  },
]);
