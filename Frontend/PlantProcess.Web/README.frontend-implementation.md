# Frontend implementation pattern

PlantProcess IQ uses thin public wrappers plus implementation files when a page or component is large enough to need staged refactoring.

Rules:

1. Public wrapper files stay small and stable for imports.
2. ".implementation.tsx" files are temporary orchestration shells, not permanent dumping grounds.
3. New extraction work should move pure helpers to sibling modules, hooks to hooks folders, and step-specific wizard content to one file per step.
4. Phase 5 file-size gate blocks unknown new god-files and reports tracked split targets.
5. The product remains generic manufacturing intelligence; steel examples are demo fixtures only.
