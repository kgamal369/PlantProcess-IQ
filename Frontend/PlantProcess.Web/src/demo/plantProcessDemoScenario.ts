export type DemoLicensePlan = "Light" | "Pro" | "ProPlus" | "Enterprise";

export type DemoConnectorStatus = "available" | "planned" | "future";

export type DemoJobStatus =
  | "Queued"
  | "Running"
  | "Completed"
  | "Warning"
  | "Failed";

export interface DemoConnector {
  id: string;
  name: string;
  provider: string;
  status: DemoConnectorStatus;
  description: string;
  availableNow: boolean;
  safeDemoLabel: string;
  tables: DemoSourceTable[];
}

export interface DemoSourceTable {
  id: string;
  name: string;
  sourceSystem: string;
  rows: number;
  lastImportedAt: string;
  freshness: "fresh" | "delayed" | "missing";
}

export interface DemoJob {
  id: string;
  name: string;
  type:
    | "Source Import"
    | "Schema Mapping"
    | "Dashboard Refresh"
    | "Risk Scoring"
    | "Report Export"
    | "ML Preview";
  status: DemoJobStatus;
  progress: number;
  lastRun: string;
  nextRun: string;
  message: string;
}

export interface DemoSchemaMapping {
  id: string;
  name: string;
  source: string;
  target: string;
  status: "Draft" | "Validated" | "Approved";
  sqlPreview: string;
  mappedFields: Array<{
    sourceField: string;
    targetField: string;
    confidence: number;
  }>;
}

export interface DemoWidget {
  id: string;
  title: string;
  type: "kpi" | "bar" | "line" | "table" | "risk" | "ml";
  requiredPlan: DemoLicensePlan;
  description: string;
  queryPreview: string;
  value?: string;
  trend?: string;
  data: Array<{
    label: string;
    value: number;
  }>;
}

export interface DemoUserRole {
  id: string;
  userName: string;
  email: string;
  role: string;
  licensePlan: DemoLicensePlan;
  status: "Active" | "Invited" | "Disabled";
  privileges: string[];
}

export interface DemoPrivilegeGroup {
  group: string;
  privileges: Array<{
    code: string;
    label: string;
    light: boolean;
    pro: boolean;
    proPlus: boolean;
    enterprise: boolean;
  }>;
}

export interface DemoMlTrainingForm {
  targetOutcome: string;
  timeWindow: string;
  validationMethod: string;
  selectedFeatures: string[];
}

export interface DemoMlPreview {
  status: "PreviewOnly";
  label: string;
  disclaimer: string;
  trainingStatus:
    | "Not Started"
    | "Configured"
    | "Training Preview"
    | "Preview Result Ready";
  modelName: string;
  resultLabel: string;
  confidence: number;
  explanation: Array<{
    feature: string;
    contribution: number;
    direction: "increases risk" | "reduces risk" | "neutral";
  }>;
  registry: Array<{
    modelName: string;
    version: string;
    status: string;
    trainedAt: string;
    note: string;
  }>;
}

export interface DemoChecklistItem {
  id: string;
  title: string;
  acceptance: string;
  priority: "Required" | "Recommended";
  done: boolean;
}

export interface DemoScreenshotItem {
  id: string;
  title: string;
  targetRoute: string;
  purpose: string;
  done: boolean;
}

export interface DemoObjection {
  objection: string;
  answer: string;
}

export interface DemoReportSection {
  title: string;
  content: string;
}

export const licensePlans: Array<{
  code: DemoLicensePlan;
  name: string;
  priceLabel: string;
  users: number | "Custom";
  sources: number | "Custom";
  dashboards: number | "Custom";
  description: string;
  features: string[];
}> = [
  {
    code: "Light",
    name: "Light",
    priceLabel: "Entry / Discovery",
    users: 3,
    sources: 1,
    dashboards: 2,
    description: "Small discovery package for limited plant visibility.",
    features: [
      "Prepared dashboards",
      "Basic data quality",
      "Basic report preview",
    ],
  },
  {
    code: "Pro",
    name: "Pro",
    priceLabel: "Team / Department",
    users: 10,
    sources: 5,
    dashboards: 10,
    description: "Operational quality intelligence for one plant team.",
    features: [
      "Dashboard builder",
      "Jobs monitor",
      "Rule-based risk score",
      "PDF report preview",
      "Basic admin preview",
    ],
  },
  {
    code: "ProPlus",
    name: "Pro Plus",
    priceLabel: "Pilot / Advanced",
    users: 25,
    sources: 10,
    dashboards: 20,
    description: "Investigation-first pilot package with correlation and workflow preview.",
    features: [
      "Correlation analysis",
      "Schema mapping",
      "Investigation workflow",
      "ML workspace preview",
      "Advanced dashboard widgets",
      "Feature matrix preview",
    ],
  },
  {
    code: "Enterprise",
    name: "Enterprise",
    priceLabel: "Custom / Private Deployment",
    users: "Custom",
    sources: "Custom",
    dashboards: "Custom",
    description: "Private deployment, custom connectors, RBAC and multi-plant rollout.",
    features: [
      "Private deployment",
      "Custom connectors",
      "Advanced RBAC",
      "Audit logs",
      "Support SLA",
      "Enterprise security review",
    ],
  },
];

export const demoConnectors: DemoConnector[] = [
  {
    id: "csv_quality",
    name: "Surface Inspection CSV",
    provider: "CSV",
    status: "available",
    availableNow: true,
    safeDemoLabel: "Available now",
    description: "Controlled CSV export from inspection system.",
    tables: [
      {
        id: "surface_defects",
        name: "surface_defects_snapshot.csv",
        sourceSystem: "Surface Inspection",
        rows: 18420,
        lastImportedAt: "2026-05-20 09:42",
        freshness: "fresh",
      },
    ],
  },
  {
    id: "postgres_process",
    name: "Process PostgreSQL Link",
    provider: "PostgreSQL",
    status: "available",
    availableNow: true,
    safeDemoLabel: "Available now / pilot",
    description: "Read-only process data source for demo workflow.",
    tables: [
      {
        id: "process_parameters",
        name: "process_parameter_observations",
        sourceSystem: "Process DB",
        rows: 932500,
        lastImportedAt: "2026-05-20 09:44",
        freshness: "fresh",
      },
      {
        id: "process_events",
        name: "process_events",
        sourceSystem: "Process DB",
        rows: 24880,
        lastImportedAt: "2026-05-20 09:44",
        freshness: "fresh",
      },
    ],
  },
  {
    id: "excel_quality",
    name: "Excel Quality Report",
    provider: "Excel",
    status: "planned",
    availableNow: false,
    safeDemoLabel: "Planned",
    description: "Preview only until Excel connector implementation and tests pass.",
    tables: [],
  },
  {
    id: "sqlserver_mes",
    name: "SQL Server MES",
    provider: "SQL Server",
    status: "planned",
    availableNow: false,
    safeDemoLabel: "Planned",
    description: "Planned connector for customer MES / L3 databases.",
    tables: [],
  },
  {
    id: "oracle_l2",
    name: "Oracle L2 / MES",
    provider: "Oracle",
    status: "planned",
    availableNow: false,
    safeDemoLabel: "Planned",
    description: "Important industrial connector, but not available until implemented and tested.",
    tables: [],
  },
  {
    id: "mysql_inspection",
    name: "MySQL Inspection Device",
    provider: "MySQL",
    status: "planned",
    availableNow: false,
    safeDemoLabel: "Planned",
    description: "Useful for inspection devices and local production databases.",
    tables: [],
  },
  {
    id: "opcua_historian",
    name: "OPC-UA / Historian",
    provider: "OPC-UA",
    status: "future",
    availableNow: false,
    safeDemoLabel: "Future",
    description: "Future live historian integration path. Do not present as active ingestion.",
    tables: [],
  },
];

export const demoJobs: DemoJob[] = [
  {
    id: "job_import_surface",
    name: "Import Surface Inspection Snapshot",
    type: "Source Import",
    status: "Completed",
    progress: 100,
    lastRun: "2026-05-20 09:42",
    nextRun: "2026-05-20 10:12",
    message: "18,420 defect rows imported into raw staging.",
  },
  {
    id: "job_import_process",
    name: "Import Process Parameter Observations",
    type: "Source Import",
    status: "Completed",
    progress: 100,
    lastRun: "2026-05-20 09:44",
    nextRun: "2026-05-20 10:14",
    message: "932,500 process observations refreshed.",
  },
  {
    id: "job_schema_mapping",
    name: "Refresh Canonical Quality Mapping",
    type: "Schema Mapping",
    status: "Completed",
    progress: 100,
    lastRun: "2026-05-20 09:46",
    nextRun: "2026-05-20 10:16",
    message: "Material, defect, equipment and event mappings validated.",
  },
  {
    id: "job_dashboard_refresh",
    name: "Refresh Executive Dashboard Widgets",
    type: "Dashboard Refresh",
    status: "Completed",
    progress: 100,
    lastRun: "2026-05-20 09:47",
    nextRun: "2026-05-20 10:17",
    message: "8 widgets refreshed from approved metadata.",
  },
  {
    id: "job_risk_scoring",
    name: "Calculate Rule-Based Quality Risk",
    type: "Risk Scoring",
    status: "Warning",
    progress: 100,
    lastRun: "2026-05-20 09:48",
    nextRun: "2026-05-20 10:18",
    message: "Elevated risk detected for Coil C-2048. Use suspected contributor wording.",
  },
  {
    id: "job_report_export",
    name: "Generate Customer Investigation Report",
    type: "Report Export",
    status: "Completed",
    progress: 100,
    lastRun: "2026-05-20 09:51",
    nextRun: "Manual",
    message: "Customer-grade report preview generated.",
  },
  {
    id: "job_ml_preview",
    name: "ML Quality Model Preview",
    type: "ML Preview",
    status: "Queued",
    progress: 0,
    lastRun: "Preview only",
    nextRun: "Manual demo action",
    message: "Frontend preview only. No trained backend model active.",
  },
];

export const demoMappings: DemoSchemaMapping[] = [
  {
    id: "map_quality_events",
    name: "Surface Defects → Quality Events",
    source: "surface_defects_snapshot.csv",
    target: "quality_events",
    status: "Validated",
    sqlPreview: `select
  defect_id as external_event_id,
  coil_no as material_code,
  defect_family as defect_type,
  severity_score as severity,
  detected_at_utc as occurred_at_utc
from raw_surface_defects
where severity_score >= 3;`,
    mappedFields: [
      { sourceField: "coil_no", targetField: "MaterialUnit.Code", confidence: 98 },
      { sourceField: "defect_family", targetField: "QualityEvent.DefectType", confidence: 95 },
      { sourceField: "severity_score", targetField: "QualityEvent.Severity", confidence: 91 },
    ],
  },
  {
    id: "map_process_parameters",
    name: "Process Observations → Canonical Parameters",
    source: "process_parameter_observations",
    target: "parameter_observations",
    status: "Approved",
    sqlPreview: `select
  material_id,
  equipment_code,
  parameter_code,
  numeric_value,
  unit,
  observed_at_utc
from raw_process_observations
where observed_at_utc >= now() - interval '7 days';`,
    mappedFields: [
      { sourceField: "equipment_code", targetField: "Equipment.Code", confidence: 97 },
      { sourceField: "parameter_code", targetField: "ParameterDefinition.Code", confidence: 94 },
      { sourceField: "numeric_value", targetField: "ParameterObservation.Value", confidence: 99 },
    ],
  },
];

export const demoWidgets: DemoWidget[] = [
  {
    id: "widget_risk",
    title: "Quality Risk Score",
    type: "risk",
    requiredPlan: "Pro",
    description: "Rule-based risk score. Not an AI prediction.",
    queryPreview: "RiskScoreService.Calculate(material, genealogy, recent events)",
    value: "72",
    trend: "+11 vs previous shift",
    data: [
      { label: "Shift A", value: 42 },
      { label: "Shift B", value: 58 },
      { label: "Shift C", value: 72 },
    ],
  },
  {
    id: "widget_defects",
    title: "Defects by Family",
    type: "bar",
    requiredPlan: "Light",
    description: "Quality event distribution from approved mapping.",
    queryPreview: "group by defect_family from quality_events",
    data: [
      { label: "Surface", value: 34 },
      { label: "Shape", value: 18 },
      { label: "Edge", value: 11 },
      { label: "Dimension", value: 7 },
    ],
  },
  {
    id: "widget_correlation",
    title: "Suspected Contributors",
    type: "bar",
    requiredPlan: "ProPlus",
    description: "Correlation-based suspected contributors. Not guaranteed root cause.",
    queryPreview: "correlate parameter windows with quality outcomes",
    data: [
      { label: "Temp instability", value: 78 },
      { label: "Speed variation", value: 63 },
      { label: "Cooling drift", value: 51 },
      { label: "Shift context", value: 29 },
    ],
  },
  {
    id: "widget_ml",
    title: "ML Preview Result",
    type: "ml",
    requiredPlan: "ProPlus",
    description: "Frontend preview only. No trained backend model active.",
    queryPreview: "Future ML inference placeholder",
    value: "Preview",
    trend: "Model registry placeholder",
    data: [
      { label: "Temperature trend", value: 36 },
      { label: "Speed variation", value: 24 },
      { label: "Material route", value: 18 },
      { label: "Inspection history", value: 12 },
    ],
  },
];

export const demoUsers: DemoUserRole[] = [
  {
    id: "u_admin",
    userName: "Demo Admin",
    email: "admin@demo.plantprocessiq.local",
    role: "Administrator",
    licensePlan: "Enterprise",
    status: "Active",
    privileges: [
      "Manage connectors",
      "Manage schema mappings",
      "Manage users",
      "View license configuration",
      "Run demo workflow",
    ],
  },
  {
    id: "u_engineer",
    userName: "Process Engineer",
    email: "engineer@demo.plantprocessiq.local",
    role: "Engineer",
    licensePlan: "ProPlus",
    status: "Active",
    privileges: [
      "View dashboard",
      "Investigate materials",
      "Run correlation preview",
      "View ML preview",
    ],
  },
  {
    id: "u_quality",
    userName: "Quality Manager",
    email: "quality@demo.plantprocessiq.local",
    role: "Quality Manager",
    licensePlan: "Pro",
    status: "Invited",
    privileges: [
      "View quality dashboards",
      "Export report preview",
      "Review suspected contributors",
    ],
  },
];

export const demoPrivilegeGroups: DemoPrivilegeGroup[] = [
  {
    group: "Dashboard & analytics",
    privileges: [
      {
        code: "dashboard.view",
        label: "View prepared dashboards",
        light: true,
        pro: true,
        proPlus: true,
        enterprise: true,
      },
      {
        code: "dashboard.builder",
        label: "Build dashboard widgets",
        light: false,
        pro: true,
        proPlus: true,
        enterprise: true,
      },
      {
        code: "correlation.view",
        label: "View correlation analysis",
        light: false,
        pro: false,
        proPlus: true,
        enterprise: true,
      },
    ],
  },
  {
    group: "Administration",
    privileges: [
      {
        code: "connectors.manage",
        label: "Configure source connectors",
        light: false,
        pro: true,
        proPlus: true,
        enterprise: true,
      },
      {
        code: "schema.manage",
        label: "Manage schema mapping",
        light: false,
        pro: false,
        proPlus: true,
        enterprise: true,
      },
      {
        code: "users.manage",
        label: "Manage users and roles",
        light: false,
        pro: false,
        proPlus: false,
        enterprise: true,
      },
    ],
  },
  {
    group: "Reports & ML preview",
    privileges: [
      {
        code: "reports.export",
        label: "Export customer-grade report",
        light: false,
        pro: true,
        proPlus: true,
        enterprise: true,
      },
      {
        code: "ml.preview",
        label: "Open ML workspace preview",
        light: false,
        pro: false,
        proPlus: true,
        enterprise: true,
      },
    ],
  },
];

export const initialMlTrainingForm: DemoMlTrainingForm = {
  targetOutcome: "Quality risk class",
  timeWindow: "Last 90 days",
  validationMethod: "Holdout validation",
  selectedFeatures: [
    "Equipment",
    "Process step",
    "Temperature trend",
    "Speed variation",
    "Material genealogy",
    "Defect history",
  ],
};

export const demoMlPreview: DemoMlPreview = {
  status: "PreviewOnly",
  label: "ML Workspace Preview — no trained model active",
  disclaimer:
    "This workflow demonstrates the future ML user experience only. Current intelligence is rule-based risk scoring, correlation analysis and suspected contributor ranking.",
  trainingStatus: "Preview Result Ready",
  modelName: "Quality Risk Preview Model",
  resultLabel: "Elevated quality risk preview",
  confidence: 78,
  explanation: [
    {
      feature: "Temperature instability before inspection",
      contribution: 36,
      direction: "increases risk",
    },
    {
      feature: "Speed variation during process step",
      contribution: 24,
      direction: "increases risk",
    },
    {
      feature: "Stable previous genealogy route",
      contribution: 14,
      direction: "reduces risk",
    },
    {
      feature: "Recent surface defect family frequency",
      contribution: 18,
      direction: "increases risk",
    },
  ],
  registry: [
    {
      modelName: "Quality Risk Preview Model",
      version: "v0-preview",
      status: "Frontend placeholder",
      trainedAt: "Not trained",
      note: "No production model active.",
    },
    {
      modelName: "Defect Family Classifier",
      version: "future",
      status: "Planned",
      trainedAt: "Future",
      note: "Requires validated historical labeled quality data.",
    },
    {
      modelName: "Root Contributor Ranking",
      version: "future",
      status: "Planned",
      trainedAt: "Future",
      note: "Will remain explainability-first and human-reviewed.",
    },
  ],
};

export const demoChecklist: DemoChecklistItem[] = [
  {
    id: "health",
    title: "API health",
    acceptance: "/health returns 200",
    priority: "Required",
    done: true,
  },
  {
    id: "db-health",
    title: "DB health",
    acceptance: "/db-health returns 200 and migration state is clean",
    priority: "Required",
    done: true,
  },
  {
    id: "login",
    title: "Demo login",
    acceptance: "Seeded demo user can log in",
    priority: "Required",
    done: true,
  },
  {
    id: "seed",
    title: "Seeded data",
    acceptance: "Dashboard, quality, risk and investigation screens show records",
    priority: "Required",
    done: true,
  },
  {
    id: "console",
    title: "Browser console",
    acceptance: "No red console errors and no failed requests during golden path",
    priority: "Required",
    done: false,
  },
  {
    id: "connector-truth",
    title: "Connector honesty",
    acceptance: "Excel, Oracle, SQL Server, MySQL and OPC-UA are not falsely available",
    priority: "Required",
    done: false,
  },
  {
    id: "language",
    title: "AI wording",
    acceptance: "No AI prediction or guaranteed root cause wording",
    priority: "Required",
    done: false,
  },
  {
    id: "report",
    title: "Customer report",
    acceptance: "Report preview is demo-ready",
    priority: "Recommended",
    done: true,
  },
];

export const screenshotPack: DemoScreenshotItem[] = [
  {
    id: "dashboard",
    title: "Command dashboard",
    targetRoute: "/dashboard",
    purpose: "Executive opening screen with KPI, quality and risk visibility.",
    done: false,
  },
  {
    id: "admin-db",
    title: "Admin DB configuration",
    targetRoute: "/admin",
    purpose: "Show source connection and connector honesty.",
    done: false,
  },
  {
    id: "admin-schema",
    title: "Schema mapping SQL preview",
    targetRoute: "/admin-preview",
    purpose: "Show mapping from plant-specific data to generic model.",
    done: false,
  },
  {
    id: "jobs",
    title: "Jobs monitor",
    targetRoute: "/admin-preview",
    purpose: "Show import, mapping, dashboard refresh and report jobs.",
    done: false,
  },
  {
    id: "ml",
    title: "ML workspace preview",
    targetRoute: "/admin-preview",
    purpose: "Show feature selection, training mock, registry and explainability.",
    done: false,
  },
  {
    id: "report",
    title: "Customer-grade report",
    targetRoute: "/admin-preview",
    purpose: "Show final investigation report preview.",
    done: false,
  },
];

export const executiveFiveMinuteScript: string[] = [
  "Position PlantProcess IQ as a generic manufacturing quality-intelligence layer.",
  "Clarify it does not replace MES, SCADA, L2 automation or BI.",
  "Open dashboard: plant health, risk, quality and data readiness.",
  "Open one quality issue and follow material genealogy to process evidence.",
  "Show suspected contributors and correlation using careful wording.",
  "Show report output, license progression and pilot/readiness next step.",
];

export const twentyMinuteScript: string[] = [
  "00:00–02:00 — Product positioning: intelligence layer, not MES/SCADA/L2 replacement.",
  "02:00–05:00 — Dashboard overview: quality, risk, readiness and defect families.",
  "05:00–08:00 — Admin connector story: CSV/PostgreSQL available, Excel/Oracle/SQL Server planned.",
  "08:00–11:00 — Schema mapping: source table to canonical quality/process model.",
  "11:00–14:00 — Jobs monitor: import, mapping, dashboard refresh and risk scoring.",
  "14:00–17:00 — Investigation-first story: event → material → genealogy → process evidence.",
  "17:00–19:00 — ML workspace preview: feature selection, training mock, registry, explainability.",
  "19:00–20:00 — Report, pricing/license progression and next-step CTA.",
];

export const objections: DemoObjection[] = [
  {
    objection: "Is this replacing our MES?",
    answer:
      "No. PlantProcess IQ sits above MES/L2/SCADA/inspection/databases as an intelligence and investigation layer. Existing systems remain the operational source.",
  },
  {
    objection: "Is this an AI system predicting defects?",
    answer:
      "Not yet. Current product intelligence is rule-based risk scoring, correlation analysis and suspected contributor ranking. ML is preview-only until trained and validated.",
  },
  {
    objection: "Can you connect to Oracle or SQL Server?",
    answer:
      "The architecture supports connector expansion. For the current demo, only tested connectors are marked available. Oracle/SQL Server are planned or custom pilot connectors.",
  },
  {
    objection: "How is this different from Power BI or Qlik?",
    answer:
      "BI visualizes data. PlantProcess IQ adds manufacturing canonical mapping, genealogy, quality-event investigation, risk scoring and workflow context before visualization.",
  },
  {
    objection: "Can the engineer trust the root cause?",
    answer:
      "The system does not claim guaranteed root cause. It presents evidence, correlations and suspected contributors for engineer validation.",
  },
];

export const customerReportSections: DemoReportSection[] = [
  {
    title: "Executive Summary",
    content:
      "Coil C-2048 shows elevated quality risk. The system highlights temperature instability and speed variation as suspected contributors. Engineering validation is required before operational changes.",
  },
  {
    title: "Affected Material",
    content:
      "Material genealogy links the affected coil to upstream heat, slab and processing route. The inspection defect family is surface-related with medium-high severity.",
  },
  {
    title: "Process Evidence",
    content:
      "Parameter windows before inspection show instability in temperature and speed trend. These are correlation-based suspected contributors, not guaranteed root cause.",
  },
  {
    title: "Recommended Next Step",
    content:
      "Run a focused process engineering review over the same parameter window, compare against known-good materials, and validate against additional historical batches.",
  },
];

export const forbiddenLanguageExamples = [
  "AI prediction",
  "guaranteed root cause",
  "automatically finds the root cause",
  "predicts defects",
  "live AI model",
];

export const safeLanguageExamples = [
  "rule-based risk score",
  "suspected contributor",
  "correlation analysis",
  "evidence-based investigation",
  "ML workspace preview only",
];