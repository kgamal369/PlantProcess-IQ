/* PPIQ-PHASE7-PRODUCT */
// Yard & Warehouse Management - evidence-grade, READ-ONLY material-flow visibility.
// Honesty contract: observes and correlates only. Issues no moves, commands no
// crane/AGV, runs no Level-2 logic, and writes nothing back to any WMS/control system.
import type { ProductPageModel } from "./model";

export const yardWarehouseProduct: ProductPageModel = {
  id: "yard-warehouse",
  slug: "yard-warehouse-management",
  name: "Yard & Warehouse Management",
  category: "Material-flow visibility",
  headline: "See every coil, slab and bay - without touching the floor.",
  subTagline:
    "Read-only material-flow visibility for the yard and finished-goods warehouse, correlated to quality outcomes.",
  problem: {
    title: "The problem",
    body:
      "Yards and finished-goods warehouses lose the thread. Location, age and status live across " +
      "spreadsheets and disconnected WMS exports, so material gets misplaced, ages past spec, and ships " +
      "late. Worse, nobody can honestly connect how a unit was stored and handled to how it performed " +
      "downstream - the storage-to-quality link is invisible.",
  },
  capabilities: [
    {
      title: "Live material map (read-only)",
      body:
        "Ingests location and status feeds and renders a read-only map of bays, racks and yard slots. " +
        "It shows where material is; it never issues a move and never commands a crane or AGV.",
    },
    {
      title: "Dwell-time & aging analysis",
      body:
        "Tracks how long each unit has sat and flags aging stock as a rule-based risk - an alert for a " +
        "human to act on, not an automated control action.",
    },
    {
      title: "Storage-to-quality correlation",
      body:
        "Correlates dwell time, storage zone and handling counts against downstream defect and quality " +
        "outcomes. Results are surfaced as suspected contributors with their population N - never proven causes.",
    },
    {
      title: "Genealogy-linked location history",
      body:
        "Ties each unit's storage and movement history into the same golden-thread genealogy used across " +
        "PlantProcess IQ, so a defect can be traced back through where and how the material was stored.",
    },
    {
      title: "Throughput & occupancy dashboards",
      body:
        "Occupancy, inbound/outbound throughput and bay utilisation as evidence dashboards, every figure " +
        "carrying its provenance handle.",
    },
  ],
  benefits: [
    { metricLabel: "Find material in seconds", body: "One read-only map replaces the spreadsheet hunt across yard and warehouse." },
    { metricLabel: "Catch aging stock early",  body: "Rule-based aging risk surfaces before a unit becomes scrap or a missed shipment." },
    { metricLabel: "Connect storage to quality", body: "Honest correlation between how material was stored and how it performed downstream." },
  ],
  diagram: {
    caption: "One-directional, read-only flow",
    nodes: ["Inbound", "Yard slots", "Warehouse racks", "Outbound", "Quality outcomes (correlated)"],
    note: "Arrows are observation only. No node is ever commanded by PlantProcess.",
  },
  licensing: {
    note: "Licensed per site; entitlements arrive from a signed license, not an editable row.",
    tiers: [
      { name: "Essentials",   includes: "Live read-only map + occupancy & throughput dashboards" },
      { name: "Professional", includes: "Adds dwell-time/aging risk + storage-to-quality correlation" },
      { name: "Enterprise",   includes: "Adds multi-site fleet view + genealogy-linked location history" },
    ],
  },
  cta: {
    heading: "See your yard the way your quality engineers wish they could.",
    body: "Request a demo on your own material data - read-only, no install on plant control systems.",
    buttonLabel: "Request a demo",
  },
  evidencePosture:
    "Read-only. PlantProcess Yard & Warehouse Management observes and correlates. It issues no commands to any " +
    "PLC or controller, runs no Level-2 logic, and writes nothing back to any WMS or plant control system.",
};