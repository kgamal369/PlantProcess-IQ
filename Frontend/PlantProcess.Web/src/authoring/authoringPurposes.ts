// PPIQ T-032. Chapter 4 section 5.2.1 - THE RULING: there is ONE authoring
// shell, not five surfaces that resemble each other. One surface whose schema
// tree, palette, validator and board semantics are parameterised by the
// purpose it was opened for. A user who learns the shell once has learned
// every authoring act in the product.
//
// This module is that parameterisation, and it is a REGISTRY: a purpose is
// added by adding a row, never by adding a branch in a component.

export type AuthoringPurpose = "S1" | "S2" | "S3" | "S4" | "S5";

// Section 5.2.2 - the toggle is always present and always offers exactly two
// modes. Neither mode is a lesser citizen.
export type AuthoringMode = "block" | "sql";

/**
 * T-038. The query contract a purpose authors, where it authors one. Exactly
 * one value exists because exactly one such contract exists: the widget query
 * expression over the canonical model, parsed and compiled by
 * IWidgetQueryExpressionService and reached through
 * executeWidgetQueryExpression. It is NOT preparation SQL, it does not touch
 * the staged catalogue, and it carries its own safety contract.
 */
export type WidgetQueryContract = "widget-query-expression";

export interface AuthoringPurposeDefinition {
  purpose: AuthoringPurpose;
  /** The name the author sees in the mode bar. */
  label: string;
  /** Section 5.2.1 column 2 - what is authored. */
  authors: string;
  /** Section 5.2.1 column 4 - the output artifact class. */
  outputArtifact: string;
  /** Section 5.2.1 column 3 - which toolbox groups this purpose presents. */
  paletteGroups: string[];
  /**
   * Section 5.2.4 - two groups in the tree on S1 ONLY, because S1's whole
   * purpose is to move data between staging and the plant schema. S2 to S5
   * show the canonical model only.
   */
  showsStagingCatalogue: boolean;
  /**
   * T-038, and it is deliberately OPTIONAL. Only S2 declares a query contract,
   * because only S2 has one today. A purpose that declares none behaves exactly
   * as it did before this field existed, which is why every other row in the
   * registry is untouched: converging S2 is not an occasion to reclassify the
   * purposes whose own tasks own them.
   */
  queryContract?: WidgetQueryContract;
}

export const AUTHORING_PURPOSES: readonly AuthoringPurposeDefinition[] = [
  {
    purpose: "S1",
    label: "Data preparation",
    authors: "Staged data filtered, joined, aliased and mapped into the plant schema",
    outputArtifact: "Transformation Definition",
    paletteGroups: ["source-output", "relational"],
    showsStagingCatalogue: true,
  },
  {
    purpose: "S2",
    label: "Widget and page binding",
    authors: "The dataset a widget displays",
    outputArtifact: "Widget definition",
    paletteGroups: ["source-output", "relational"],
    showsStagingCatalogue: false,
    queryContract: "widget-query-expression",
  },
  {
    purpose: "S3",
    label: "Analysis authoring",
    authors: "Correlation, statistics, mathematics",
    outputArtifact: "Analysis Definition",
    paletteGroups: ["source-output", "relational", "statistics"],
    showsStagingCatalogue: false,
  },
  {
    purpose: "S4",
    label: "Model authoring",
    authors: "Model-based analyses over the same canonical data",
    outputArtifact: "Model Definition",
    paletteGroups: ["source-output", "relational", "model-feature"],
    showsStagingCatalogue: false,
  },
  {
    purpose: "S5",
    label: "Plant data log",
    authors: "Rules emitting info, warning and error entries",
    outputArtifact: "Rule Definition",
    paletteGroups: ["source-output", "condition-action"],
    showsStagingCatalogue: false,
  },
];

export function purposeDefinition(purpose: AuthoringPurpose): AuthoringPurposeDefinition {
  const found = AUTHORING_PURPOSES.find((p) => p.purpose === purpose);
  if (!found) {
    // Unreachable through the type, reachable through a bad cast at a call
    // site. Refusing loudly beats rendering a shell with no palette at all.
    throw new Error("Unknown authoring purpose: " + String(purpose));
  }
  return found;
}