export type DemoLicensePlan = "Light" | "Pro" | "ProPlus" | "Enterprise";

export type DemoWorkflowStepStatus =
  | "not-started"
  | "running"
  | "completed"
  | "warning"
  | "locked";

export interface DemoConnector {
  id: string;
  name: string;
  provider: string;
  status: "available" | "planned" | "future";
  description: string;
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
  type: "Source Import" | "Schema Mapping" | "Dashboard Refresh" | "Risk Scoring" | "ML Preview";
  status: "Queued" | "Running" | "Completed" | "Warning" | "Failed";
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
  type: "kpi" | "bar" | "line" | "table" | "risk";
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

export interface DemoMlPreview {
  selectedFeatures: string[];
  trainingStatus: "Not Started" | "Configured" | "Training Preview" | "Preview Result Ready";
  modelName: string;
  resultLabel: string;
  confidence: number;
  explanation: Array<{
    feature: string;
    contribution: number;
    direction: "increases risk" | "reduces risk" | "neutral";
  }>;
}

export interface DemoUserRole {
  id: string;
  userName: string;
  role: string;
  licensePlan: DemoLicensePlan;
  status: "Active" | "Invited" | "Disabled";
  privileges: string[];
}

export const licensePlans: Array<{
  code: DemoLicensePlan;
  name: string;
  price: string;
  users: number | "Custom";
  sources: number | "Custom";
  dashboards: number | "Custom";
  description: string;
  features: string[];
}> = [
  {
    code: "Light",
    name: "Light",
    price: "Entry",
    users: 3,
    sources: 1,
    dashboards: 2,
    description: "Small discovery package for limited visibility.",
    features: [
      "Prepared dashboards",
      "Basic data quality",
      "Basic report preview",
    ],
  },
  {
    code: "Pro",
    name: "Pro",
    price: "Team",
    users: 10,
    sources: 5,
    dashboards: 10,
    description: "Operational quality intelligence for one team.",
    features: [
      "Dashboard builder",
      "Jobs monitor",
      "Rule-based risk score",
      "PDF report preview",
    ],
  },
  {
    code: "ProPlus",
    name: "Pro Plus",
    price: "Pilot",
    users: 25,
    sources: 10,
    dashboards: 20,
    description: "Investigation-first pilot package.",
    features: [
      "Correlation analysis",
      "Schema mapping",
      "Investigation workflow",
      "ML workspace preview",
      "Advanced dashboard widgets",
    ],
  },
  {
    code: "Enterprise",
    name: "Enterprise",
    price: "Custom",
    users: "Custom",
    sources: "Custom",
    dashboards: "Custom",
    description: "Private deployment and custom plant integration.",
    features: [
      "Private deployment",
      "Custom connectors",
      "Advanced RBAC",
      "Audit logs",
      "Support SLA",
    ],
  },
];

export const demoConnectors: DemoConnector[] = [
  {
    id: "csv_quality",
    name: "Surface Inspection CSV",
    provider: "CSV",
    status: "available",
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
    description: "Preview only until Excel connector implementation is tested.",
    tables: [],
  },
  {
    id: "oracle_mes",
    name: "Oracle MES / L2",
    provider: "Oracle",
    status: "planned",
    description: "Planned custom connector for industrial MES/L2 systems.",
    tables: [],
  },
  {
    id: "opcua_historian",
    name: "OPC-UA / Historian",
    provider: "OPC-UA",
    status: "future",
    description: "Future live historian integration path.",
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
    message: "High risk detected for Coil C-2048. Use suspected contributor wording.",
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
    description: "Rule-based risk score. Not AI prediction.",
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
    type: "risk",
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

export const demoMlPreview: DemoMlPreview = {
  selectedFeatures: [
    "Equipment",
    "Process step",
    "Temperature trend",
    "Speed variation",
    "Material genealogy",
    "Defect history",
  ],
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
};