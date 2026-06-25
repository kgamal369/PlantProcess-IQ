# PlantProcess IQ -- Architecture and Deployment

| | |
|---|---|
| Document class | Technical Architecture |
| Audience | IT, infrastructure, security |
| Product | PlantProcess IQ (PPIQ) -- SOU Industrial Software |
| Version | 1.0 -- June 2026 |

## Architecture at a glance

PlantProcess IQ is composed of three logical tiers.

1. **Edge collector.** A customer-controlled component that reads from the plant's sources and pushes data outward, one way, toward the core. The core never initiates a connection into the plant or control network. For the most sensitive environments a one-way data-diode topology is supported.
2. **Core platform.** The analytical engines, the canonical mapping and genealogy model, the KPI and dashboard services, the job scheduler, and the application API. All computation over plant data occurs inside the tenant.
3. **Presentation layer.** A browser-based interface (the operator and engineering console) served over TLS, plus generated reports.

Data flows in one direction: source to edge collector to staging to canonical model to analysis to presentation. No analytical or presentation component holds a path back into the plant.

## Deployment models

| Model | Description | Typical use |
|---|---|---|
| Multi-tenant SaaS | Hosted by the vendor; tenant isolation enforced by identifier plus row-level security | Fastest to start; lower operational burden |
| Dedicated | A physically isolated instance per customer | Stronger isolation requirements |
| On-premise | Deployed inside the customer's own data center | Data-residency or policy requirements |
| Air-gapped | Installed with no outbound network dependency | High-security or disconnected sites |

The same product serves every model; the deployment topology is a configuration choice, not a separate code base.

## Network and runtime posture

The application is served behind a reverse proxy terminating TLS. The database listens only on the loopback interface and is not publicly exposed. Health and readiness endpoints are provided for monitoring and for orchestrated rollout. A deployment that fails its health gate rolls back to the previous known-good release rather than remaining in a broken state.

## Sizing and system requirements

Reference sizing tiers are provided for the edge collector and the core platform based on source count, data volume, and retention horizon, and are supplied as part of a technical proposal. A representative single-plant workload is on the order of several hundred production heats and several thousand finished coils per month; the platform is dimensioned for that scale with progress indication and table virtualization so that interface responsiveness is preserved as data grows.

## Operations

Backup and restore procedures are documented and drilled. Import, analysis, and maintenance jobs are observable through the jobs monitor. A clean installation reaches a working login by following the provided runbook, without bespoke intervention.