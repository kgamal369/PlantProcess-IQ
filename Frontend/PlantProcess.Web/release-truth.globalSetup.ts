// Associative certification global setup.
// Backlog origin: T-204   Release: M2   Owner: Worker 2 (Release Truth)
// One evidence run per execution, cleared first, so a stale phase artifact can
// never be mistaken for a current observation.

import { createRunDirectory as createRouteRun } from "./e2e/release-truth/durableRouteEvidence";
import { createRunDirectory } from "./e2e/release-truth/associativeSelectionEvidence";

export default function globalSetup(): void {
  createRouteRun();
  const dir = createRunDirectory();
  // eslint-disable-next-line no-console
  console.log(`[release-truth] associative evidence run directory: ${dir}`);
}