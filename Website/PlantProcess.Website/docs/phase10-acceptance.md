
# PlantProcess IQ — Phase 10 Website Acceptance Matrix



## P10-01 Product ecosystem pages



Implemented pages:



- `/product` — PlantProcess IQ

- `/products/mes` — MES

- `/products/qes` — QES

- `/products/yard` — Yard & Warehouse Management

- `/products/energy` — Energy Management



Each product story includes:



- description

- business benefit

- interactive workflow graphic

- license detail

- clear inquiry CTA



## P10-02 Pricing / License + Security / Trust



Implemented pages:



- `/pricing`

- `/security`



The pricing page uses the existing Light / Pro / Pro Plus / Enterprise plan data and adds a usage-feature matrix.



The security page states:



- read-only source layer

- data handling model

- deployment models

- AI honesty

- enterprise controls



## P10-03 Demo request CTA + lead capture



Implemented by:



- `src/components/proof/RequestDemoForm.tsx`



Lead behavior:



- validates required fields

- scores customer fit

- stores captured leads in localStorage key `ppiq.website.demoLeads.v1`

- exposes a Commercial Admin local lead queue

- prepares a notification email draft



## P10-04 Brand + accessibility audit



Implemented by:



- `scripts/validate-website-content.mjs`

- `src/styles/phase10.css`



Checks:



- brand tokens

- product page content

- pricing/security content

- lead capture implementation

- alt text presence in App

- forbidden honesty claims



## P10-05 Website test pack



Implemented by:



- `scripts/phase10-website-e2e.mjs`

- package scripts `e2e`, `test:phase10`, `validate:phase10`



Main command:



```bash

npm run validate:phase10
