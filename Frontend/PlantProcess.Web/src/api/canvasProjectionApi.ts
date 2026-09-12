import { apiClient } from "./http";
import type { CanonicalProjectionField } from "./canvasApi";

// PPIQ T-262. THE WRITABLE FIELDS OF A GOVERNED OUTPUT TARGET.
//
// Its own module rather than another export on canvasApi, because it is consumed by
// exactly one surface - the output mapping inspector - and a module boundary keeps the
// authoring client from growing into one file every test has to know about.
//
// The browser keeps NO field list. An entity the catalogue does not recognise answers
// with nothing, and the surface says so rather than offering something plausible.
export type OutputTargetFields = {
  entity: string;
  source: string;
  fields: CanonicalProjectionField[];
};

export const listOutputTargetFields = (entity: string) =>
  apiClient.get<OutputTargetFields>(
    `/api/prep/authoring/output-targets/${encodeURIComponent(entity)}/fields`);