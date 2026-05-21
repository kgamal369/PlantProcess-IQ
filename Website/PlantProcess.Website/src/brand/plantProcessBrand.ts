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
  proofAssets: [
    {
      title: "Engineer brief",
      href: "/brand/plantprocess-iq-engineer-brief.html",
      description:
        "Technical one-page brief explaining product scope, architecture, non-goals, and demo lifecycle.",
    },
    {
      title: "Architecture diagram",
      href: "/brand/plantprocess-iq-architecture.svg",
      description:
        "Read-only source → staging → canonical model → analytics → report architecture.",
    },
    {
      title: "Screenshot gallery",
      href: "/#screenshots",
      description:
        "Screenshots for dashboard, admin configuration, schema mapping, jobs monitor, ML readiness, and report preview.",
    },
  ],
} as const;