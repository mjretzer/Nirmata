import js from "@eslint/js";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";

export default tseslint.config(
  // Base JS recommended rules
  js.configs.recommended,

  // TypeScript recommended (type-aware disabled to stay fast)
  ...tseslint.configs.recommended,

  // React Hooks rules — the most critical lint for this project
  {
    plugins: { "react-hooks": reactHooks },
    rules: {
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
    },
  },

  // Project-wide overrides
  {
    rules: {
      // Allow unused vars prefixed with _
      "@typescript-eslint/no-unused-vars": [
        "warn",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" },
      ],
      // Allow `any` during dev transition (tighten later)
      "@typescript-eslint/no-explicit-any": "off",
      // Prefer `interface` but don't enforce
      "@typescript-eslint/consistent-type-definitions": "off",
      // Allow empty functions (event handler stubs, etc.)
      "@typescript-eslint/no-empty-function": "off",
    },
  },

  // Ignore build output and generated files
  {
    ignores: ["dist/", "node_modules/", "*.config.*"],
  }
);
