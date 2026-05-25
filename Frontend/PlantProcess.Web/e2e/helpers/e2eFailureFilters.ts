// ============================================================
// FILE: Frontend/PlantProcess.Web/e2e/helpers/e2eFailureFilters.ts
//
// Shared E2E failure filtering.
// We still fail on real page runtime errors.
// We do not fail just because the diagnostics telemetry beacon itself failed.
// ============================================================

import type { ConsoleMessage, Request, Response } from "@playwright/test";

export type AllowedFailureOptions = {
  allowServerFailureUrlFragments?: string[];
  allowClientFailureUrlFragments?: string[];
};

export function isTelemetryUrl(url: string): boolean {
  return url.includes("/diagnostics/client-error");
}

export function isAllowedUrl(url: string, fragments: string[] = []): boolean {
  return fragments.some((fragment) => url.includes(fragment));
}

export function isIgnorableConsoleMessage(message: ConsoleMessage): boolean {
  const text = message.text();

  if (/failed to load resource/i.test(text)) {
    return true;
  }

  if (/download the react devtools/i.test(text)) {
    return true;
  }

  return false;
}

export function shouldTrackFailedRequest(
  request: Request,
  options: AllowedFailureOptions = {}
): boolean {
  const url = request.url();

  if (isTelemetryUrl(url)) return false;
  if (isAllowedUrl(url, options.allowClientFailureUrlFragments)) return false;
  if (isAllowedUrl(url, options.allowServerFailureUrlFragments)) return false;

  return true;
}

export function shouldTrackFailedResponse(
  response: Response,
  options: AllowedFailureOptions = {}
): boolean {
  const status = response.status();
  const url = response.url();

  if (status < 400) return false;
  if (isTelemetryUrl(url)) return false;
  if (isAllowedUrl(url, options.allowClientFailureUrlFragments)) return false;
  if (isAllowedUrl(url, options.allowServerFailureUrlFragments)) return false;

  return true;
}

export function formatRequestFailure(request: Request): string {
  return `${request.method()} ${request.url()}`;
}

export function formatResponseFailure(response: Response): string {
  return `${response.status()} ${response.request().method()} ${response.url()}`;
}