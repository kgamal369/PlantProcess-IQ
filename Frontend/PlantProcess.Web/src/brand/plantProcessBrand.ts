export const plantProcessBrand = {
  productName: "PlantProcess IQ",
  companyName: "SOU Industrial Intelligence",
  founderLocation: "Düsseldorf, Germany",
  marketFocus: "EU / MENA industrial plants",
  tagline: "Connect plant data. Understand your process. Act with evidence.",
  shortPositioning:
    "A read-only manufacturing intelligence layer for process-to-quality investigation.",
  longPositioning:
    "PlantProcess IQ connects plant data, creates a safe staging copy, maps customer-specific schemas into a generic canonical model, and supports dashboards, risk scoring, correlation, investigation, ML readiness, and Data Diagnostic reporting.",
  category:
    "Generic manufacturing quality-intelligence and process-to-quality investigation platform",
  notClaims: [
    "Not MES",
    "Not SCADA",
    "Not Level 2 replacement",
    "Not PLC control",
    "Not BI-only",
    "Not steel-only",
  ],
  approvedLanguage: [
    "Rule-based risk scoring",
    "Statistical correlation",
    "Suspected contributor ranking",
    "Evidence-based investigation",
    "ML readiness",
    "Data Diagnostic",
    "Read-only intelligence layer",
    "Generic manufacturing canonical model",
  ],
  forbiddenLanguage: [
    "Guaranteed root cause",
    "AI prediction",
    "Production-ready AI",
    "Live Oracle connector today",
    "Live MSSQL connector today",
    "MES replacement",
    "SCADA replacement",
    "L2 replacement",
  ],
  industries: [
    "Flat steel",
    "Long steel",
    "Aluminum",
    "Paper",
    "Pharma",
    "Food",
    "Chemicals",
    "Automotive",
    "Tires",
  ],
  connectors: [
    {
      name: "CSV snapshot",
      status: "Available now",
      message: "Safe starter connector for controlled demos and diagnostics.",
    },
    {
      name: "Excel snapshot",
      status: "Proof required",
      message: "Show only after end-to-end proof passes.",
    },
    {
      name: "PostgreSQL read-only",
      status: "Available for Pro+ when environment proof is green",
      message: "Use for read-only database integration demos.",
    },
    {
      name: "SQL Server",
      status: "Planned",
      message: "Do not show as available until implementation and tests pass.",
    },
    {
      name: "Oracle",
      status: "Planned",
      message: "Important for steel plants, but must remain honest as planned.",
    },
    {
      name: "MySQL",
      status: "Planned",
      message: "Useful for QMS/inspection systems; not available yet.",
    },
  ],
  colors: {
    deepNavy: "#020712",
    navy: "#04101f",
    panel: "#071a2d",
    cyan: "#13d8ff",
    cyanSoft: "#72e7ff",
    green: "#2df7a3",
    amber: "#ffb020",
    red: "#ff4d6d",
    text: "#eaf7ff",
    muted: "#91a9c5",
  },
} as const;

export type PlantProcessBrand = typeof plantProcessBrand;