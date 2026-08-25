// ============================================================================
// Release-truth global setup.
//
// Backlog origin: T-203   Release: M2   Owner: Worker 2 (Release Truth)
//
// Creates exactly one route-evidence run directory per gate execution and
// clears any previous run, so a stale artifact can never be mistaken for a
// current observation.
// ============================================================================

import { createRunDirectory } from "./e2e/release-truth/durableRouteEvidence";

export default function globalSetup(): void {
  const dir = createRunDirectory();
  // eslint-disable-next-line no-console
  console.log(`[release-truth] route evidence run directory: ${dir}`);
}