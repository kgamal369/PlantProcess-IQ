export type RouteContractSeverity = "critical" | "important" | "normal";

export type RouteContract = {
  route: string;
  name: string;
  severity: RouteContractSeverity;
  expectedText: RegExp;
  mustRefreshSafely: boolean;
  customerVisible: boolean;
};

export const phase1RouteContracts: RouteContract[] = [
  {
    route: "/dashboard",
    name: "Dashboard",
    severity: "critical",
    expectedText: /dashboard|widget|quality|risk|plantprocess iq/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
  {
    route: "/material-investigation",
    name: "Material Investigation",
    severity: "critical",
    expectedText: /material|investigation|genealogy|quality/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
  {
    route: "/risk",
    name: "Risk Dashboard",
    severity: "important",
    expectedText: /risk|score|quality|prediction|readiness/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
  {
    route: "/data-quality",
    name: "Data Quality",
    severity: "important",
    expectedText: /data quality|issue|completeness|validity/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
  {
    route: "/correlation",
    name: "Correlation",
    severity: "important",
    expectedText: /correlation|parameter|quality|signal/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
  {
    route: "/admin",
    name: "Admin",
    severity: "critical",
    expectedText: /admin|configuration|connector|schema|import|job/i,
    mustRefreshSafely: true,
    customerVisible: false,
  },
  {
    route: "/demo-lifecycle",
    name: "Demo Lifecycle",
    severity: "critical",
    expectedText: /demo|lifecycle|golden|reset|report/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
  {
    route: "/ml-readiness",
    name: "ML Readiness",
    severity: "important",
    expectedText: /ml|readiness|label|model|correlation/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
  {
    route: "/commercial-license",
    name: "Commercial License",
    severity: "important",
    expectedText: /license|light|pro|enterprise|usage/i,
    mustRefreshSafely: true,
    customerVisible: true,
  },
];

export function getCriticalRouteContracts() {
  return phase1RouteContracts.filter((x) => x.severity === "critical");
}