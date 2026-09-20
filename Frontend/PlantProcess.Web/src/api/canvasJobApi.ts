import { apiClient } from "./http";

// PPIQ T-245. THE CANVAS SIDE OF THE GOVERNED JOB MODEL.
//
// Every call addresses a definition by its CODE. The browser never sends a job id, a
// definition id or a run id as authority: the server resolves identity from the caller's
// tenant and refuses anything that does not belong to it.
//
// There is no client-side scheduler, no client-side run list and no client-derived
// outcome. What a run did is what the server says it did.

const BASE = "/api/prep/definitions";

// staticallyEligible is a statement about what is knowable BEFORE dispatch. It reserves
// nothing: a launch resolves the version again and may still refuse, which is why the
// server sends its own assurance sentence rather than letting a surface invent one.
export type CanvasExecutionCapability = {
  definitionCode: string;
  definitionId: string;
  targetKind: string;
  resolvedVersion: number | null;
  versionPolicy: string;
  familyIsExecutable: boolean;
  executorRegistered: boolean;
  staticallyEligible: boolean;
  refusalCode: string | null;
  refusalDetail: string | null;
  assurance: string;
};

export type CanvasJobBinding = {
  jobDefinitionId: string;
  jobCode: string;
  jobName: string;
  definitionId: string;
  definitionCode: string;
  targetKind: string;
  versionPolicy: string;
  pinnedVersion: number | null;
  created: boolean;
};

export type CanvasRunBlock = {
  blockId: string;
  executionOrdinal: number;
  status: string;
  inputRows: number | null;
  outputRows: number | null;
  diagnosticCode: string | null;
  diagnosticDetail: string | null;
  startedAtUtc: string | null;
  finishedAtUtc: string | null;
};

export type CanvasRunEvidence = {
  runId: string;
  jobDefinitionId: string;
  jobCode: string;
  status: string;
  isTerminal: boolean;
  startedAtUtc: string;
  completedAtUtc: string | null;
  correlationId: string | null;
  targetDefinitionId: string | null;
  targetDefinitionVersion: number | null;
  targetDefinitionKind: string | null;
  targetVersionPolicy: string | null;
  cancellationRequested: boolean;
  cancellationRequestedAtUtc: string | null;
  cancellationAcknowledgedAtUtc: string | null;
  failureReason: string | null;
  runMessage: string | null;
  blocks: CanvasRunBlock[];
};

export const describeExecution = (code: string, version?: number) =>
  apiClient.get<CanvasExecutionCapability>(
    `${BASE}/${encodeURIComponent(code)}/execution-capability`
      + (version === undefined ? "" : `?version=${version}`));

export const bindCanvasJob = (code: string, body: { pinnedVersion?: number | null; jobName?: string | null }) =>
  apiClient.post<CanvasJobBinding>(`${BASE}/${encodeURIComponent(code)}/job-binding`, body);

export const getCanvasJobBinding = (code: string) =>
  apiClient.get<CanvasJobBinding>(`${BASE}/${encodeURIComponent(code)}/job-binding`);

// The launch carries the correlation the monitor will attach to. The server owns the
// execution from that moment: this request completing, failing or being abandoned by a
// closed tab does not decide what the run does.
export const launchCanvasRun = (code: string, body: { correlationId: string; requestedBy?: string | null }) =>
  apiClient.post<CanvasRunEvidence>(`${BASE}/${encodeURIComponent(code)}/runs`, body);

// Returns null while the run does not exist yet. That is an answer, not an error, and it
// is never satisfied with somebody else's newest run.
export const findCanvasRun = (code: string, correlationId: string) =>
  apiClient.get<CanvasRunEvidence | null>(
    `${BASE}/${encodeURIComponent(code)}/runs?correlationId=${encodeURIComponent(correlationId)}`);

export const readCanvasRun = (code: string, runId: string) =>
  apiClient.get<CanvasRunEvidence>(`${BASE}/${encodeURIComponent(code)}/runs/${encodeURIComponent(runId)}`);

export const requestCanvasRunCancellation = (jobDefinitionId: string, runId: string, body: { requestedBy?: string | null; reason?: string | null }) =>
  apiClient.post<unknown>(`/admin/jobs/${jobDefinitionId}/runs/${runId}/cancel`, body);
