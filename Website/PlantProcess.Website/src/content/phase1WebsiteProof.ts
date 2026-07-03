
export type WebsiteConnectorStatus =
  | "available-now"
  | "demo-certified"
  | "implemented-certification-pending"
  | "planned"
  | "simulated-source-shape";

export type WebsiteConnector = {
  provider: string;
  label: string;
  status: WebsiteConnectorStatus;
  frontendLabel: string;
  note: string;
};

export type WebsiteLicensePlan = {
  code: "standard" | "proPlus" | "enterprise";
  name: string;
  deposit: string;
  monthlyPrice: string;
  recommended: boolean;
  idealFor: string;
  users: string;
  sources: string;
  refresh: string;
  connectors: string;
  features: string[];
  cta: string;
};

export const websiteConnectors: WebsiteConnector[] = [
  {
    provider: "csv",
    label: "CSV",
    status: "available-now",
    frontendLabel: "Available now",
    note: "Best for controlled exports, first diagnostics, and rapid discovery.",
  },
  {
    provider: "excel",
    label: "Excel",
    status: "available-now",
    frontendLabel: "Available now",
    note: "Good for lab files, QA sheets, business checks, and manual investigations.",
  },
  {
    provider: "postgresql",
    label: "PostgreSQL",
    status: "demo-certified",
    frontendLabel: "Runs live in the demo environment",
    note: "Read-only connector running against a live PostgreSQL source system in the certified demo environment.",
  },
  {
    provider: "sqlserver",
    label: "Microsoft SQL Server",
    status: "demo-certified",
    frontendLabel: "Runs live in the demo environment",
    note: "Read-only connector running against a live SQL Server source in the certified demo environment. Typical fit: MES, QA and ERP-side databases.",
  },
  {
    provider: "mysql",
    label: "MySQL",
    status: "demo-certified",
    frontendLabel: "Runs live in the demo environment",
    note: "Read-only connector running against live MySQL sources in the certified demo environment. Typical fit: inspection systems, downtime databases, device-side systems.",
  },
  {
    provider: "oracle",
    label: "Oracle",
    status: "demo-certified",
    frontendLabel: "Runs live in the demo environment",
    note: "Read-only connector running against live Oracle source databases in the certified demo environment. Typical fit: process automation and tracking systems.",
  },
  {
    provider: "rest-api",
    label: "REST API",
    status: "planned",
    frontendLabel: "Planned",
    note: "For API-based systems after the database and file connector workflow is proven with the first customers.",
  },
];

export const licensePlans: WebsiteLicensePlan[] = [
  {
    code: "standard",
    name: "Standard",
    deposit: "$12k one-time deposit",
    monthlyPrice: "$6k / month",
    recommended: false,
    idealFor: "Plants that want to connect their data and finally see it in one place.",
    users: "Core quality and process team",
    sources: "Up to 3 data sources",
    refresh: "Scheduled imports",
    connectors: "CSV, Excel, and one certified database engine",
    features: [
      "Read-only connection to your existing systems",
      "Two-stage import: staging first, then a unified canonical model",
      "Dashboards and configurable pages built from the HMI",
      "KPI views and risk indicators",
      "Material investigation and genealogy views",
    ],
    cta: "Start with Standard",
  },
  {
    code: "proPlus",
    name: "Pro Plus",
    deposit: "$28k one-time deposit",
    monthlyPrice: "$14k / month",
    recommended: true,
    idealFor: "Plants that want to move from seeing their data to reasoning about it.",
    users: "Multi-team site usage",
    sources: "Up to 6 data sources",
    refresh: "Frequent scheduled imports",
    connectors: "CSV, Excel, and all certified database engines",
    features: [
      "Everything in Standard",
      "Correlation engine with disciplined statistics on your own data",
      "AI/ML suggestion engine with evidence-ranked recommendations",
      "Alerts and inspection jobs on defects, downtime, and KPIs",
      "Data quality visibility and reporting pack",
    ],
    cta: "Request Pro Plus pilot",
  },
  {
    code: "enterprise",
    name: "Enterprise",
    deposit: "$50k one-time deposit",
    monthlyPrice: "$25k / month",
    recommended: false,
    idealFor: "Plant groups and sites that want the full intelligence layer, at scale.",
    users: "Custom, multi-site",
    sources: "Unlimited data sources and custom pages",
    refresh: "Custom SLA and deployment design",
    connectors: "All connectors plus custom connector hardening",
    features: [
      "Everything in Pro Plus",
      "Grounded AI assistant: ask questions, get cited answers",
      "Multi-site rollout and governed data model extension",
      "Advanced security, audit alignment, and access governance",
      "Priority support and a pilot-to-production roadmap",
    ],
    cta: "Talk to us",
  },
];

export const positioningTruths = [
  {
    title: "Not MES",
    text: "PlantProcess IQ does not replace order execution, production booking, or plant transaction systems.",
  },
  {
    title: "Not SCADA",
    text: "It does not control machines, alarms, PLCs, or operator control screens.",
  },
  {
    title: "Not Level 2",
    text: "It does not replace automation models, setup calculation, or real-time process control.",
  },
  {
    title: "Not BI-only",
    text: "It goes beyond dashboards by adding staging, unified mapping, genealogy, correlation, AI/ML suggestions and a grounded AI assistant.",
  },
];

export const provenAtScale = [
  { value: "11,997", label: "Material units unified into one canonical model" },
  { value: "1,993", label: "Quality events linked to materials and process steps" },
  { value: "5,688", label: "Genealogy links walked in both directions" },
  { value: "6", label: "Live source systems across 4 database engines" },
];

export const requestDemoMail = "info@plantprocessiq.com";
