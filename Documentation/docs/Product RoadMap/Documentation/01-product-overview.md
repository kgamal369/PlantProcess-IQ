# PlantProcess IQ -- Product Overview

| | |
|---|---|
| Document class | Product Overview |
| Audience | All stakeholders |
| Product | PlantProcess IQ (PPIQ) -- SOU Industrial Software |
| Version | 1.0 -- June 2026 |

## The problem

A modern plant already generates the data needed to understand its own quality and process behavior. The difficulty is that the data is fragmented across many systems -- a Level 2 tracking database, inspection and surface-defect devices, a historian, laboratory results, planning and yard systems -- each with its own structure, its own keys, and its own idea of what an object is. A single finished product carries a genealogy that crosses every one of those systems, but no one system can walk that thread end to end. Engineers reconstruct it by hand, in spreadsheets, one investigation at a time.

## The approach

PlantProcess IQ connects to those existing sources as a read-only intelligence layer, builds one canonical view of the plant's material genealogy from the customer's own business keys, and lets engineers investigate quality and downtime questions against that unified picture. It is generic by design: the same software serves any plant by configuration, not by code. Every plant differs in database types, table structures, inspection devices, line layouts, processes, and in the parameters and KPIs its engineers care about -- so nothing about a specific plant is built into the product.

The product follows a maturity curve. Simple analysis and dashboards are available from day one. More advanced findings -- parameter-to-defect correlations, downtime drivers, KPI contributors -- become available as data-readiness gates are satisfied, and loading historical data brings those gates forward rather than waiting months for them to fill naturally.

## Who it is for

- The **process and quality engineer** investigating why a defect appears, who needs to move faster than a spreadsheet and to trust what the tool tells them.
- The **reliability and plant-administration user** who onboards the data sources, owns the import and analysis jobs, and keeps the platform running.
- The **executive sponsor** who needs a quantified, defensible view of where quality loss and downtime originate, with each role seeing the scope appropriate to it.

## Where the boundary lies

PlantProcess IQ is explicit about what it is and is not.

- It is a **read-only** layer. It never writes to, commands, or changes a source or control system.
- It performs **correlation analysis and evidence-based investigation**. It identifies suspected contributors and statistical patterns; it does not claim a guaranteed root cause.
- It **connects and explains** the data a plant's systems produce. It is not a Manufacturing Execution System, a Level 2 controller, a SCADA system, or a business-intelligence reporting tool, and it does not replace them.
- Every analytical result carries its **population, its method, and its exclusions**. Where the data is not yet sufficient, the product shows a readiness state instead of inventing an answer.

This boundary is a feature. It is what allows a plant's automation and security teams to approve the product, and it is what makes its findings trustworthy to the engineer who depends on them.