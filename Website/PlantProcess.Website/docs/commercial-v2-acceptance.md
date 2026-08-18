# SOU Industrial Software Commercial Website — Acceptance Contract

The commercial website is accepted only when all three executable gates are green:

1. Production build: `npm run build`.
2. Commercial source-truth gate: `npm run validate:commercial:v2`.
3. Browser certification: `npm run test:commercial:e2e`.

The active public architecture is **SOU Industrial Software with five independent industrial software products**:

- PlantProcess IQ — Plant intelligence — **flagship**.
- Manufacturing Execution System — Plant execution.
- Quality Execution System — Quality execution.
- Yard and Warehouse Management — Material flow.
- Energy Management System — Resource efficiency.

PlantProcess IQ is a sibling product and the flagship; it is **not** the parent or container of the other four products. PPIQ capability packs may exist within the PlantProcess IQ product experience, but sibling product routes must never redirect into those packs.

## Commercial truth required across the website

- Company identity: `SOU Industrial Software`.
- Corporate domain: `souindustrial.com`.
- Public corporate email: `info@souindustrial.com`.
- Locations use the correct display spelling: `Düsseldorf, Germany · Alexandria, Egypt`.
- Founder identity: `Karim Gamal`.
- Experience statement: 14 years of industrial engineering experience; stale 13-year wording is rejected.
- PlantProcess IQ remains read-only with no control-system write-back.
- Connector availability is confirmed per source class; unproven connectors are presented as planned, not supported.
- Public fixed installation/subscription prices are not asserted; commercial pricing is configured per deployment/quotation.
- Root SEO describes SOU Industrial Software; the PlantProcess IQ route keeps product-specific metadata.
- Unsupported autonomous-root-cause/control/write-back claims are forbidden.

## Browser acceptance

Desktop and mobile certification must prove:

- one visible H1, visible header and footer, and no horizontal overflow;
- no unnamed buttons or empty links;
- the portfolio exposes all five canonical product routes;
- `/product` redirects only to `/products/plantprocess-iq`;
- canonical sibling product routes stay on their own product pages and do not redirect into `/packs/*`;
- the About page carries the approved founder, 14-year statement, and `Düsseldorf` spelling;
- pricing contains no retired fixed public price ranges;
- the Contact page exposes the approved corporate address `info@souindustrial.com`;
- the lead-capture form posts to `/api/v5/outbound/leads` and renders the success state.

Evidence screenshots are written below the configured `PPIQ_COMMERCIAL_EVIDENCE_DIR` (default `test-results/commercial-v2`).

**Operational note:** website-code certification does not prove external mailbox delivery. Public operational release of `info@souindustrial.com` still requires a real external send/receive test through the configured email-routing provider.