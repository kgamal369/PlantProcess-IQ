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
  code: "light" | "pro" | "proPlus" | "enterprise";
  name: string;
  monthlyPrice: string;
  idealFor: string;
  users: string;
  tokens: string;
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
    status: "implemented-certification-pending",
    frontendLabel: "Implemented / certification pending",
    note: "Read-only source connector implementation exists; demo certification requires environment smoke test.",
  },
  {
    provider: "sqlserver",
    label: "Microsoft SQL Server",
    status: "implemented-certification-pending",
    frontendLabel: "Implemented / certification pending",
    note: "Target connector for MES, QA, ERP-side and manufacturing source databases.",
  },
  {
    provider: "mysql",
    label: "MySQL",
    status: "implemented-certification-pending",
    frontendLabel: "Implemented / certification pending",
    note: "Useful for inspection systems, downtime databases, device-side systems and small MES sources.",
  },
  {
    provider: "oracle",
    label: "Oracle",
    status: "simulated-source-shape",
    frontendLabel: "Oracle-shaped source demo",
    note: "Represented in Phase 1 as Oracle-shaped source schemas until real Oracle connector certification is complete.",
  },
  {
    provider: "rest-api",
    label: "REST API",
    status: "planned",
    frontendLabel: "Planned",
    note: "For API-based systems after database and file connector workflow is proven.",
  },
];

export const licensePlans: WebsiteLicensePlan[] = [
  {
    code: "light",
    name: "Light",
    monthlyPrice: "Starter diagnostic",
    idealFor: "Discovery workshops and early data-readiness checks.",
    users: "2–3 users",
    tokens: "Low monthly analysis allowance",
    refresh: "Manual / scheduled low-frequency refresh",
    connectors: "CSV + Excel",
    features: [
      "Connector truth view",
      "Basic staging/import visibility",
      "Limited dashboards",
      "Customer-safe PDF diagnostic",
      "No advanced automation",
    ],
    cta: "Start diagnostic",
  },
  {
    code: "pro",
    name: "Pro",
    monthlyPrice: "Pilot team",
    idealFor: "Plant quality, process and data teams proving the workflow.",
    users: "5–10 users",
    tokens: "Medium analysis allowance",
    refresh: "Scheduled refresh",
    connectors: "CSV, Excel, PostgreSQL/MSSQL/MySQL when certified",
    features: [
      "Golden workflow demo",
      "Schema mapping workbench",
      "Widget builder",
      "Scheduled imports",
      "Material investigation views",
    ],
    cta: "Request pilot",
  },
  {
    code: "proPlus",
    name: "Pro Plus",
    monthlyPrice: "Multi-team pilot",
    idealFor: "Manufacturing sites that need broader dashboard and investigation usage.",
    users: "10–25 users",
    tokens: "Higher analysis allowance",
    refresh: "More frequent scheduled refresh",
    connectors: "Certified DB connectors + source-shaped diagnostics",
    features: [
      "More dashboards and widgets",
      "Advanced widget expression layer",
      "Data quality story",
      "Risk scoring workspace",
      "Customer-grade reporting pack",
    ],
    cta: "Plan rollout",
  },
  {
    code: "enterprise",
    name: "Enterprise",
    monthlyPrice: "Custom",
    idealFor: "Plant groups, multi-site pilots, and controlled production rollout.",
    users: "Custom",
    tokens: "Custom",
    refresh: "Custom SLA and deployment design",
    connectors: "Roadmap-based connector certification",
    features: [
      "Private deployment path",
      "Custom connector hardening",
      "Governed data model extension",
      "Security and audit alignment",
      "Pilot-to-product roadmap",
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
    text: "It goes beyond dashboards by adding staging, mapping, genealogy, investigation and risk-readiness logic.",
  },
];

export const requestDemoMail = "info@plantprocessiq.com";