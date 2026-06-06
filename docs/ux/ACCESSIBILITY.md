# PlantProcess IQ Accessibility Approach

Phase 6 target: WCAG 2.1 AA for the application shell and key workflows.

## Theme and contrast

- Dark and light theme tokens are centralized in 'src/styles/phase56/phase56-tokens.css'.
- TypeScript constants are centralized in 'src/design-system/phase56/phase56Tokens.ts'.
- The persisted theme key is 'plantprocess.theme.v1'.
- The runtime honors 'prefers-color-scheme' until the user explicitly selects a theme.

## Keyboard and focus

- A skip-to-content link is injected by 'phase56ThemeRuntime.ts'.
- Focus styling uses ':focus-visible' with the AA-safe focus token.
- Escape dispatches a standard 'phase56-escape-dismiss' event for future modals/wizards.

## Reduced motion and color independence

- 'prefers-reduced-motion: reduce' suppresses nonessential animation and transitions.
- Status and state components should expose text/icon labels, not color-only meaning.

## Gates

- 'node tools/phase56/contrast-check.cjs' validates core AA contrast pairs.
- 'node tools/phase56/check-file-size-complexity.cjs' blocks unknown new god-files.
- 'npm run test:a11y' runs the Playwright Phase 5/6 shell accessibility journey when the app is available.
