// Governed aggregate/window binding for Canvas.
//
// This module owns NO aggregation vocabulary and NO window catalogue.
// It consumes canonical metadata/options produced elsewhere and copies only
// published authority into node data. Missing aggregation is AG01. Nothing
// silently becomes Average, and nothing invents a window.
//
// T-245 later compiles these validated declarations onto governed runtime
// contracts. This module deliberately performs no analytics mathematics.

export type RegistryRefusal = {
  readonly code?: string;
  readonly errorCode?: string;
  readonly message?: string;
};

export type CanonicalMeasureMetadata = {
  readonly code?: string;
  readonly aggregation?: string | null;
  readonly signalCode?: string | null;
};

export type CanonicalRegistryMetadata = {
  readonly measures?: readonly CanonicalMeasureMetadata[];
  readonly refusals?: readonly RegistryRefusal[];
};

export type GovernedAggregateBinding =
  | {
      readonly ok: true;
      readonly data: {
        readonly measureCode: string;
        readonly aggregation: string;
        readonly signalCode?: string;
      };
    }
  | {
      readonly ok: false;
      readonly refusal: {
        readonly code: "AG01" | "MEASURE_AUTHORITY_UNDECLARED";
        readonly message: string;
      };
    };

function clean(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

export function bindGovernedAggregate(
  metadata: CanonicalRegistryMetadata,
  measureCode: string,
): GovernedAggregateBinding {
  const code = clean(measureCode);
  const measures = Array.isArray(metadata.measures) ? metadata.measures : [];
  const measure = measures.find((item) => clean(item.code) === code);

  if (!measure) {
    return {
      ok: false,
      refusal: {
        code: "MEASURE_AUTHORITY_UNDECLARED",
        message: "The selected measure is not published by canonical registry metadata.",
      },
    };
  }

  const refusal = (Array.isArray(metadata.refusals) ? metadata.refusals : [])
    .find((item) => clean(item.code) === code && clean(item.errorCode) === "AG01");
  const aggregation = clean(measure.aggregation);

  if (refusal || aggregation.length === 0) {
    return {
      ok: false,
      refusal: {
        code: "AG01",
        message: clean(refusal?.message) || "aggregation_semantics_undeclared",
      },
    };
  }

  const signalCode = clean(measure.signalCode);
  return {
    ok: true,
    data: {
      measureCode: code,
      aggregation,
      ...(signalCode ? { signalCode } : {}),
    },
  };
}

export type GovernedWindowOption = {
  readonly code?: string;
};

export type GovernedWindowBinding =
  | { readonly ok: true; readonly data: { readonly windowCode: string } }
  | {
      readonly ok: false;
      readonly refusal: {
        readonly code: "WINDOW_AUTHORITY_UNDECLARED";
        readonly message: string;
      };
    };

/**
 * The option list must come from a governed producer. This binder refuses an
 * unknown/empty code; it never supplies a default window.
 */
export function bindGovernedWindow(
  options: readonly GovernedWindowOption[],
  requestedCode: string,
): GovernedWindowBinding {
  const code = clean(requestedCode);
  const found = options.find((item) => clean(item.code) === code);
  if (!found || code.length === 0) {
    return {
      ok: false,
      refusal: {
        code: "WINDOW_AUTHORITY_UNDECLARED",
        message: "The selected window is not declared by the governed window authority.",
      },
    };
  }
  return { ok: true, data: { windowCode: code } };
}
