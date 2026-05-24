/**
 * utils/errorMapping.ts
 * --------------------------------------------------------------------
 * Translate HTTP status codes and network errors into human-friendly
 * toast messages. Centralised here so every caller is consistent.
 *
 * Used by the API client (apiClient.ts) and by component error handlers.
 */

export interface FriendlyError {
  /** Headline for the toast. */
  headline: string;
  /** Optional second line — typically the endpoint or a hint. */
  description?: string;
  /** Toast severity. */
  severity: "error" | "warning" | "info";
  /** Whether the caller should retry (the API client uses this). */
  retryable: boolean;
}

export interface ErrorMappingInput {
  status: number;          // 0 for network/abort
  responseText?: string;   // raw response body if available
  method?: string;         // HTTP method
  path?: string;           // endpoint path
}

/**
 * Try to extract a useful message from a JSON error payload.
 * The backend ApplicationResult shape is { message, errorType } —
 * we surface .message when present.
 */
function extractMessage(responseText?: string): string | undefined {
  if (!responseText) return undefined;
  try {
    const parsed = JSON.parse(responseText);
    if (typeof parsed?.message === "string" && parsed.message.length > 0) {
      return parsed.message;
    }
    if (typeof parsed?.detail === "string" && parsed.detail.length > 0) {
      return parsed.detail;
    }
    if (typeof parsed?.title === "string" && parsed.title.length > 0) {
      return parsed.title;
    }
  } catch {
    // Not JSON — fall through
  }
  // If the responseText is short and looks like a message, use it.
  if (responseText.length > 0 && responseText.length < 200) {
    return responseText;
  }
  return undefined;
}

export function mapErrorToFriendly(input: ErrorMappingInput): FriendlyError {
  const backendMessage = extractMessage(input.responseText);
  const endpoint = input.path ? `${input.method ?? ""} ${input.path}`.trim() : undefined;

  // Network / abort / offline — status 0
  if (input.status === 0) {
    return {
      headline: "Network problem",
      description: "Please check your connection and try again.",
      severity: "warning",
      retryable: true,
    };
  }

  // Validation
  if (input.status === 400 || input.status === 422) {
    return {
      headline: backendMessage ?? "Invalid input",
      description: endpoint,
      severity: "warning",
      retryable: false,
    };
  }

  // Authentication
  if (input.status === 401) {
    return {
      headline: "Your session expired",
      description: "Please sign in again to continue.",
      severity: "warning",
      retryable: false,
    };
  }

  // Authorization / licensing
  if (input.status === 403) {
    return {
      headline: backendMessage ?? "This action is not available on your licence tier",
      description: endpoint,
      severity: "warning",
      retryable: false,
    };
  }

  // Not found
  if (input.status === 404) {
    return {
      headline: backendMessage ?? "This item is no longer available",
      description: endpoint,
      severity: "info",
      retryable: false,
    };
  }

  // Conflict
  if (input.status === 409) {
    return {
      headline: backendMessage ?? "This record was updated by someone else",
      description: "Refresh to see the latest version.",
      severity: "warning",
      retryable: false,
    };
  }

  // Rate limited
  if (input.status === 429) {
    return {
      headline: "Too many requests",
      description: "Please wait a moment and try again.",
      severity: "warning",
      retryable: true,
    };
  }

  // Server errors
  if (input.status >= 500 && input.status < 600) {
    return {
      headline: "Something went wrong on our side",
      description: endpoint,
      severity: "error",
      retryable: false,
    };
  }

  // Fallback for any other status
  return {
    headline: backendMessage ?? `Request failed (${input.status})`,
    description: endpoint,
    severity: "error",
    retryable: false,
  };
}
