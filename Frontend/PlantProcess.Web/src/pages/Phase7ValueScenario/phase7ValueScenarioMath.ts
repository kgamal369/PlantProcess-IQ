
export type LooseApiObject = Record<string, unknown>;

export type ScenarioRange = {
  low: number;
  expected: number;
  high: number;
};

export type ScenarioImpactView = ScenarioRange & {
  currency: string;
  isAbstained: boolean;
  abstainReason: string | null;
  caveat: string;
};

export type ScenarioRealizationView = ScenarioRange & {
  currency: string;
  status: string;
  improvementUnits: number;
  captureRateMid: number | null;
  roiMid: number | null;
  caveat: string;
  isAbstained: boolean;
};

export function readNumber(source: LooseApiObject | null | undefined, ...keys: string[]): number {
  if (!source) return 0;

  for (const key of keys) {
    const value = source[key];
    if (typeof value === "number" && Number.isFinite(value)) return value;
    if (typeof value === "string" && value.trim() !== "" && Number.isFinite(Number(value))) {
      return Number(value);
    }
  }

  return 0;
}

export function readString(source: LooseApiObject | null | undefined, fallback: string, ...keys: string[]): string {
  if (!source) return fallback;

  for (const key of keys) {
    const value = source[key];
    if (typeof value === "string" && value.trim() !== "") return value;
  }

  return fallback;
}

export function readBoolean(source: LooseApiObject | null | undefined, ...keys: string[]): boolean {
  if (!source) return false;

  for (const key of keys) {
    const value = source[key];
    if (typeof value === "boolean") return value;
  }

  return false;
}

export function normalizeImpact(source: LooseApiObject | null): ScenarioImpactView {
  return {
    currency: readString(source, "EUR", "currency", "Currency"),
    low: readNumber(source, "low", "Low"),
    expected: readNumber(source, "expected", "Expected", "mid", "Mid"),
    high: readNumber(source, "high", "High"),
    isAbstained: readBoolean(source, "isAbstained", "IsAbstained"),
    abstainReason: readString(source, "", "abstainReason", "AbstainReason") || null,
    caveat: readString(
      source,
      "Projected value range only; not a guaranteed saving.",
      "honestyCaveat",
      "HonestyCaveat"
    ),
  };
}

export function normalizeRealization(source: LooseApiObject | null): ScenarioRealizationView {
  return {
    currency: readString(source, "EUR", "currency", "Currency"),
    low: readNumber(source, "realizedLow", "RealizedLow"),
    expected: readNumber(source, "realizedExpected", "RealizedExpected", "realizedMid", "RealizedMid"),
    high: readNumber(source, "realizedHigh", "RealizedHigh"),
    status: readString(source, "NotCalculated", "status", "Status"),
    improvementUnits: readNumber(source, "improvementUnits", "ImprovementUnits"),
    captureRateMid: readNullableNumber(source, "captureRateMid", "CaptureRateMid"),
    roiMid: readNullableNumber(source, "roiMid", "RoiMid"),
    caveat: readString(
      source,
      "Baseline-vs-actual tracked value is not automatic causal attribution.",
      "attributionCaveat",
      "AttributionCaveat"
    ),
    isAbstained: readBoolean(source, "isAbstained", "IsAbstained"),
  };
}

export function readNullableNumber(source: LooseApiObject | null | undefined, ...keys: string[]): number | null {
  if (!source) return null;

  for (const key of keys) {
    const value = source[key];
    if (value === null || value === undefined) continue;
    if (typeof value === "number" && Number.isFinite(value)) return value;
    if (typeof value === "string" && value.trim() !== "" && Number.isFinite(Number(value))) {
      return Number(value);
    }
  }

  return null;
}

export function formatMoney(value: number, currency = "EUR"): string {
  return new Intl.NumberFormat("en", {
    style: "currency",
    currency,
    maximumFractionDigits: 0,
  }).format(value);
}

export function workedCaseLocalProjection(): ScenarioRange {
  const affectedTons = 0.02 * 10_000;

  return {
    low: affectedTons * 140,
    expected: affectedTons * 210,
    high: affectedTons * 280,
  };
}
