export type ActionMatrixItem = {
  id: string;
  route: string;
  labelRegex: RegExp;
  expectedBehavior: "visible" | "enabled-if-data" | "safe-disabled" | "optional";
  severity: "critical" | "important" | "normal";
  notes: string;
};

export const phase1ActionMatrix: ActionMatrixItem[] = [
  {
    id: "dashboard-add-widget",
    route: "/dashboard",
    labelRegex: /add widget|create widget|widget/i,
    expectedBehavior: "visible",
    severity: "critical",
    notes: "Customer must be able to see or open the widget builder from dashboard.",
  },
  {
    id: "dashboard-refresh",
    route: "/dashboard",
    labelRegex: /refresh|reload/i,
    expectedBehavior: "optional",
    severity: "important",
    notes: "Refresh action should not crash if dashboard data is temporarily unavailable.",
  },
  {
    id: "admin-refresh",
    route: "/admin",
    labelRegex: /refresh|reload/i,
    expectedBehavior: "optional",
    severity: "critical",
    notes: "Admin page should expose safe refresh where available.",
  },
  {
    id: "admin-run",
    route: "/admin",
    labelRegex: /run|run now|execute|retry/i,
    expectedBehavior: "optional",
    severity: "critical",
    notes: "Job/import actions must be customer-safe and toast mapped.",
  },
  {
    id: "admin-preview",
    route: "/admin",
    labelRegex: /preview|test|validate/i,
    expectedBehavior: "optional",
    severity: "critical",
    notes: "Schema/query preview should fail safely, never white-screen.",
  },
  {
    id: "demo-reset",
    route: "/demo-lifecycle",
    labelRegex: /reset|seed|golden/i,
    expectedBehavior: "optional",
    severity: "critical",
    notes: "Demo reset should be controlled and visible only in demo context.",
  },
  {
    id: "demo-report",
    route: "/demo-lifecycle",
    labelRegex: /report|pdf|export|download/i,
    expectedBehavior: "visible",
    severity: "critical",
    notes: "Customer-grade report action should be visible or route should show controlled unavailable state.",
  },
  {
    id: "license-upgrade",
    route: "/commercial-license",
    labelRegex: /upgrade|request|enterprise|pro plus/i,
    expectedBehavior: "optional",
    severity: "important",
    notes: "Commercial license actions must not dead-end silently.",
  },
];