// T-094. The declared-dimension wire contract, frontend side.
//
// ADDITIVE IN STAGE 2B-i. These types describe what the backend already accepts
// and returns after stage 2A; no existing payload changes shape because every
// new member is optional. The consumers switch to them in stage 2B-ii.

/**
 * A filter on a customer-declared dimension, keyed by its published code. The
 * same shape the backend accepts on both the widget and workspace surfaces.
 */
export interface DeclaredDimensionFilter {
  code: string;
  value: string;
}

/**
 * A dimension the tenant has PUBLISHED. The consumer renders what the registry
 * declares; the product compiles no dimension vocabulary of its own.
 *
 * isExecutable false means the declaration exists but cannot be bound, and
 * refusalCode says why. Such a dimension is offered as unavailable rather than
 * hidden, so an author can see that the declaration exists and what is wrong.
 */
export interface DeclaredDimensionReference {
  code: string;
  label: string;
  dataType: string;
  grainCode: string;
  isExecutable: boolean;
  refusalCode?: string | null;
}

/** The repeatable query parameter carrying one keyed declared filter. */
export const DECLARED_FILTER_PARAM = "dimensionFilter";

/** The separator between a published code and its value. */
export const DECLARED_FILTER_SEPARATOR = ":";

/**
 * Why a keyed filter written on the wire could not be read.
 *
 * FAIL CLOSED. A filter this client cannot parse is a refusal, never a silent
 * drop: dropping it would widen the population and report the wider number as
 * the answer. The code mirrors the backend's DB09 so one vocabulary describes
 * the same fault on both sides.
 */
export class DeclaredFilterFormatError extends Error {
  public readonly refusalCode = "DB09_dimension_filter_malformed";
  public readonly entry: string;

  constructor(entry: string, message: string) {
    super(message);
    this.name = "DeclaredFilterFormatError";
    this.entry = entry;
  }
}

/** The wire form of one keyed filter: code, separator, value. */
export function formatDeclaredFilterParam(filter: DeclaredDimensionFilter): string {
  return filter.code + DECLARED_FILTER_SEPARATOR + filter.value;
}

/**
 * Read one wire entry. The FIRST separator splits, so a value may itself contain
 * one. A malformed entry throws rather than returning null, because a caller
 * that receives null is free to ignore it and that is the silent widening this
 * contract exists to prevent.
 */
export function parseDeclaredFilterParam(entry: string): DeclaredDimensionFilter {
  const raw = (entry ?? "").trim();

  const separator = raw.indexOf(DECLARED_FILTER_SEPARATOR);
  if (separator <= 0 || separator === raw.length - 1) {
    throw new DeclaredFilterFormatError(
      raw,
      "A declared-dimension filter must be written as code" + DECLARED_FILTER_SEPARATOR + "value."
    );
  }

  const code = raw.slice(0, separator).trim();
  const value = raw.slice(separator + 1).trim();

  if (!code || !value) {
    throw new DeclaredFilterFormatError(
      raw,
      "A declared-dimension filter carries both a published code and a value."
    );
  }

  return { code, value };
}