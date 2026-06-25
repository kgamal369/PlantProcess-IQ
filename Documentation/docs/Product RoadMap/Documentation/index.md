# PlantProcess IQ -- Product Documentation

| | |
|---|---|
| Product | PlantProcess IQ (PPIQ) -- SOU Industrial Software |
| Version | 1.0 -- June 2026 |
| Status | Released |

PlantProcess IQ is a generic, read-only, evidence-grade process-to-quality intelligence platform for manufacturing plants. Each plant connects its own data sources, defines its own mappings, builds its own pages, monitors its own jobs, and investigates its own quality, downtime, and KPI questions -- carried from fragmented raw data to trustworthy understanding, without being asked to take a black box on faith.

PlantProcess IQ does not replace the systems a plant already runs. It is a read-only intelligence layer that connects to the data those systems produce and explains it. The product is deliberately built around correlation analysis and evidence-based investigation -- it surfaces suspected contributors with their population and method, never a guaranteed conclusion.

## How this documentation is organized

| Document | For whom | Answers |
|---|---|---|
| [Product Overview](01-product-overview.md) | Everyone | What PlantProcess IQ is, the problem it solves, and where its boundary lies |
| [Platform Capabilities](02-platform-capabilities.md) | Evaluators, engineers | What the platform does, feature by feature |
| [Architecture and Deployment](03-architecture-and-deployment.md) | IT, infrastructure | How it is built and how it deploys (SaaS, dedicated, on-premise, air-gapped) |
| [Security and Data Protection](04-security-and-data-protection.md) | Security, OT, procurement | Read-only guarantees, identity, secrets, isolation, encryption, audit, AI data boundary |
| [Data Onboarding and Integration](05-data-onboarding-and-integration.md) | Plant IT, DBAs | How to connect sources and map them, safely |
| [Analytics Methodology and Trust](06-analytics-methodology-and-trust.md) | Process and quality engineers, executives | How findings are produced and why they can be trusted |

## The honesty contract

Every analysis in PlantProcess IQ states its population and exclusions, names the method used, and frames a result as a suspected contributor rather than a proven cause. Where data is insufficient, the product reports a readiness state rather than a fabricated answer. This posture is intentional and is described in detail in the methodology document.