/* PPIQ-PHASE7-PRODUCT */
// Manufacturing Execution (MES) Integration - evidence-grade, READ-ONLY.
// Honesty contract: PlantProcess reads the MES; it is not a SCADA system, runs no
// Level-2 logic, issues no commands to any PLC, and writes nothing back. Your MES
// stays the system of record and control.
import type { ProductPageModel } from "./model";

export const mesProduct: ProductPageModel = {
  id: "mes-integration",
  slug: "manufacturing-execution-integration",
  name: "Manufacturing Execution (MES) Integration",
  category: "Execution data, read-only",
  headline: "We read your MES. We don't replace it.",
  subTagline:
    "Evidence-grade, read-only integration with your existing MES - turning execution data into quality intelligence.",
  problem: {
    title: "The problem",
    body:
      "Your MES holds the execution truth - orders, routings, work-order genealogy, process events - but " +
      "quality engineers can't easily correlate any of it to defects. Ripping out or swapping the MES is " +
      "risky and political. Teams don't need another control system on the floor; they need intelligence on " +
      "top of the one they already trust.",
  },
  capabilities: [
    {
      title: "Read-only MES connectors",
      body:
        "Pulls orders, routings, work-order genealogy and process events from the existing MES over " +
        "read-only interfaces. PlantProcess is a reader on the bus, never a writer.",
    },
    {
      title: "Execution-to-quality correlation",
      body:
        "Correlates execution parameters - cycle time, route, station, crew - against quality outcomes as " +
        "suspected contributors with population N and FDR control. Never a proven cause.",
    },
    {
      title: "Unified genealogy",
      body:
        "Folds MES work-order genealogy into the cross-system golden thread, so a defective unit traces " +
        "cleanly from quality result back through execution and material.",
    },
    {
      title: "Execution event timeline",
      body:
        "A read-only execution timeline shown alongside quality events, so an investigation has both views " +
        "in one place.",
    },
    {
      title: "Strictly no OT control",
      body:
        "PlantProcess does not schedule, dispatch, command or write back. The MES remains the system of " +
        "record and the system of control; PlantProcess only explains.",
    },
  ],
  benefits: [
    { metricLabel: "Keep your MES", body: "Add quality intelligence without a risky execution-system swap." },
    { metricLabel: "Honest correlation", body: "Execution parameters surfaced as suspected contributors, never as automatic root cause." },
    { metricLabel: "One genealogy", body: "MES work-order lineage joins the cross-system golden thread." },
  ],
  diagram: {
    caption: "One-directional, read-only integration",
    nodes: ["MES (system of record & control)", "Read-only connector", "PlantProcess IQ (correlation + genealogy)", "Evidence dashboards"],
    note: "Data flows MES -> PlantProcess only. Nothing flows back to the MES or the floor.",
  },
  licensing: {
    note: "Licensed per connected MES instance; entitlements arrive from a signed license.",
    tiers: [
      { name: "Essentials",   includes: "Read-only orders/routings + execution timeline" },
      { name: "Professional", includes: "Adds execution-to-quality correlation" },
      { name: "Enterprise",   includes: "Adds unified cross-system genealogy + multi-line fleet view" },
    ],
  },
  cta: {
    heading: "Turn the MES you already run into quality intelligence.",
    body: "Request a demo on a read-only export of your execution data - no change to your MES.",
    buttonLabel: "Request a demo",
  },
  evidencePosture:
    "Read-only. The MES Integration reads execution data and correlates it. It is not a SCADA system, runs no " +
    "Level-2 logic, issues no commands to any PLC, and writes nothing back to the MES or any control system.",
};