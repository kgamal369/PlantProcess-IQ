export type FrontendDiagnosticLevel = "log" | "warn" | "error" | "debug" | "trace";

export type FrontendDiagnosticDetailFactory = () => readonly unknown[];

export function recordFrontendDiagnostic(
  level: FrontendDiagnosticLevel,
  source: string,
  detailsFactory?: FrontendDiagnosticDetailFactory
): void {
  if (typeof window === "undefined") {
    return;
  }

  let details: readonly unknown[] = [];

  if (detailsFactory) {
    try {
      details = detailsFactory();
    } catch {
      details = ["diagnostic-detail-factory-failed"];
    }
  }

  const diagnostic = {
    level,
    source,
    details: details.map((item) => safeDiagnosticValue(item)),
    timestampUtc: new Date().toISOString(),
  };

  window.dispatchEvent(
    new CustomEvent("ppiq:frontend-diagnostic", {
      detail: diagnostic,
    })
  );

  if (typeof performance !== "undefined" && typeof performance.mark === "function") {
    try {
      performance.mark("ppiq-diagnostic:" + level + ":" + source);
    } catch {
      // Browser performance marks can reject long/custom names. Diagnostics must never break UI flow.
    }
  }
}

function safeDiagnosticValue(value: unknown): string {
  if (value instanceof Error) {
    return value.name + ": " + value.message;
  }

  if (typeof value === "string") {
    return value;
  }

  if (typeof value === "number" || typeof value === "boolean" || typeof value === "bigint") {
    return String(value);
  }

  if (value === null || value === undefined) {
    return String(value);
  }

  try {
    return JSON.stringify(value);
  } catch {
    return Object.prototype.toString.call(value);
  }
}
