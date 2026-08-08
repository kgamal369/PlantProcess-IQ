/* PPIQ-T075 */
import type { AssistantCitation, AssistantWidgetResultEvidence } from "@/api/assistantApi";

/**
 * T-075. The pure half of the evidence surface: state shape, strip fields,
 * navigation target and starter questions.
 *
 * Kept free of React and of the network so every rule below is testable without
 * a browser, and so the difference between "this evidence is unavailable" and
 * "the request failed" is a value rather than a rendering accident.
 */
export type EvidenceState =
  | { status: "loading" }
  | { status: "loaded"; evidence: AssistantWidgetResultEvidence }
  | { status: "unavailable"; reason: string }
  | { status: "failed"; reason: string };

/** The kind whose evidence has a resolvable detail endpoint. */
export const WIDGET_RESULT_KIND = "WidgetResult";

export function citationKey(citation: AssistantCitation): string {
  return citation.kind + ":" + citation.id;
}

/**
 * The chip label comes from the evidence handle and NEVER from the answer text.
 * A friendly name invented from prose would be a claim about the source that
 * the source did not make.
 */
export function chipLabel(citation: AssistantCitation): string {
  const short = citation.id.length > 8 ? citation.id.slice(0, 8) : citation.id;
  return citation.kind + " \u00b7 " + short;
}

export interface StripField {
  label: string;
  value: string;
}

/**
 * Readable fields, in reading order, and ONLY those the resolved evidence
 * genuinely supplies. Nothing is relabelled: observationCount is presented as
 * observationCount, because that is what the result contract calls it.
 */
export function stripFields(evidence: AssistantWidgetResultEvidence): StripField[] {
  const fields: StripField[] = [
    { label: "Evidence kind", value: WIDGET_RESULT_KIND },
    { label: "Evidence id", value: evidence.evidenceId },
    { label: "Page", value: evidence.pageCode },
    { label: "Widget", value: evidence.widgetCode },
  ];

  if (evidence.measureCode) fields.push({ label: "Measure", value: evidence.measureCode });
  if (evidence.dimensionCode) fields.push({ label: "Dimension", value: evidence.dimensionCode });
  if (evidence.chartType) fields.push({ label: "Chart", value: evidence.chartType });
  if (evidence.generatedAtUtc) fields.push({ label: "As of", value: evidence.generatedAtUtc });

  if (evidence.hasObservationCount) {
    fields.push({
      label: "observationCount total",
      value: String(evidence.observationCountTotal),
    });
  }

  const filters = (evidence.filterContext ?? "").trim();
  if (filters.length > 0 && filters !== "{}") {
    fields.push({ label: "Filter context", value: filters });
  }

  return fields;
}

/**
 * The navigation target for widget-result evidence.
 *
 * The canonical route already exists - /workspace/:dashboardCode - and T-073's
 * pageCode IS the real dashboard code, so this resolves an identity rather than
 * assembling a string that looks plausible. The widget travels as a query
 * parameter on that same route; no new route is invented.
 *
 * Returns null when the evidence carries no page, because a link that cannot be
 * honoured should not be offered.
 */
export function openInPageHref(evidence: AssistantWidgetResultEvidence): string | null {
  const page = (evidence.pageCode ?? "").trim();
  if (page.length === 0) return null;

  const widget = (evidence.widgetCode ?? "").trim();
  const base = "/workspace/" + encodeURIComponent(page);

  return widget.length > 0 ? base + "?focusWidget=" + encodeURIComponent(widget) : base;
}

export interface StarterContext {
  pageCode?: string | null;
  widgetCode?: string | null;
  selections?: readonly string[];
}

/**
 * Three context-derived starters, or fewer, or none.
 *
 * Every noun comes from the CURRENT context - the page code, the focused widget
 * code, a live selection - and never from a compiled list. There is deliberately
 * no fallback set of demo questions: when there is nothing real to ask about,
 * the surface says so instead of inventing something to fill the space.
 */
export function starterQuestions(context: StarterContext): string[] {
  const widget = (context.widgetCode ?? "").trim();
  const page = (context.pageCode ?? "").trim();
  const selections = (context.selections ?? []).filter((s) => s.trim().length > 0);

  if (widget.length > 0) {
    const starters = [
      "What does " + widget + " show?",
      "What evidence supports " + widget + "?",
    ];

    if (selections.length > 0) {
      starters.push("What does " + widget + " show for " + selections[0] + "?");
    } else if (page.length > 0) {
      starters.push("What evidence is available on " + page + "?");
    }

    return starters.slice(0, 3);
  }

  if (page.length > 0) {
    return [
      "What evidence is available on " + page + "?",
      "Which findings apply to " + page + "?",
    ];
  }

  return [];
}

/** Shown when there is no real context to build a question from. */
export const NO_STARTER_PROMPT = "Select a widget to ask about its evidence.";