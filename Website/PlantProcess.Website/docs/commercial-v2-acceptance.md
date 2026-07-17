# PlantProcess IQ Commercial Website v2 — Acceptance Contract

The commercial website is accepted only when all of the following are green:

1. Production build (`npm run build`).
2. Commercial source truth gate (`npm run validate:commercial:v2`).
3. Browser certification (`npm run test:commercial:e2e`) across desktop and mobile viewports.
4. No horizontal overflow, duplicate H1, unnamed control, empty link, dead legacy route, or unsupported commercial claim.
5. Evidence screenshots generated under `test-results/commercial-v2/<timestamp>/screenshots`.

The active public story is one PlantProcess IQ read-only core with Quality/Surface, Reliability/Downtime, Energy Intelligence, and Yard/Logistics capability packs. Legacy `/products/*` URLs remain as redirects only.
