# PlantProcess IQ -- Data Onboarding and Integration

| | |
|---|---|
| Document class | Integration Guide |
| Audience | Plant IT, DBAs, integration engineers |
| Product | PlantProcess IQ (PPIQ) -- SOU Industrial Software |
| Version | 1.0 -- June 2026 |

This guide describes how a plant's data sources are connected to PlantProcess IQ and mapped into a canonical model, and what guarantees apply during that process.

## 1. Connecting a source

In the administration interface, the database-configuration page provides a database-link tab. To add a source, an administrator creates a link, selects the provider, and the system tests connectivity before the link is saved. Credentials are entered once and are masked thereafter. The administrator then selects which tables to import and sets a synchronization cadence per object, from a few minutes to several days, with off-hour windows where appropriate.

## 2. Supported source types

PlantProcess IQ is designed to connect to Microsoft SQL Server, PostgreSQL, MySQL, Oracle, and file-based sources such as Excel and CSV, with no change to the product. The connector catalog reports its certified availability per source and version honestly, so that a source still being certified is shown as such rather than presented as fully ready.

## 3. Staging and delta logic

Imported data is held in a staging area in its original, source-shaped form. On each scan, the system compares the last index already held against the source's current last index and imports only new records. Every run writes an import batch recording rows, duration, the watermark reached, and any errors, so that progress and history are always visible.

## 4. Mapping and business-key reconciliation

Once data is staged, an administrator authors mapping views that fit the plant's structure into the canonical model. Joins begin from business keys. Where two systems identify the same object differently, a business-key dictionary reconciles them explicitly and rejects genuine conflicts rather than merging them silently. Mapping is authored as read-only views in tiered modes, so that the common case requires no hand-written SQL while expert authoring remains available. KPIs are defined as versioned views rather than being fixed in code.

## 5. Backfill of history

Loading historical data is supported from a database export, a replica, or off-peak range reads. Backfill is throttled to honor the source-impact budget, is idempotent and watermark-tracked, and can be paused and resumed. Its progress is visible in the jobs monitor. Loading history brings analytical readiness forward, so that advanced findings become available sooner.

## 6. Schema change and mapping health

When a source column is added, renamed, or removed, the system raises a typed schema-change event. Dependent mapping views are flagged and dependent imports are paused rather than allowed to produce incorrect facts. A mapping-health panel reports green, degraded, or broken status with the reason and the next safe step. Bad joins produce precise, typed errors rather than silent or generic failures.

## 7. The read-only contract during integration

At no point during connection, import, mapping, or backfill does PlantProcess IQ write to, command, or alter a source. All access is read-only, bounded by row caps, statement timeouts, and approved windows. This contract is described in full in the security and data-protection document.