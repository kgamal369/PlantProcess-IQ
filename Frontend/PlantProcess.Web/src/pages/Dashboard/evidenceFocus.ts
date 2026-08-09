/* PPIQ-T075 */

/**
 * T-075. Focusing the widget an evidence citation came from.
 *
 * Deliberately tiny and DOM-level rather than a second dashboard focus
 * framework: the workspace has no focus affordance today, so this is the
 * smallest thing that looks final - find the real widget, bring it into view,
 * mark it briefly. It reads the DOM and touches nothing else. No data, no
 * filter and no widget configuration is altered.
 */
export const EVIDENCE_FOCUS_CLASS = "ppiq-evidence-focus";

/** The attribute the workspace stamps on each rendered widget. */
export const WIDGET_CODE_ATTRIBUTE = "data-widget-code";

export function findWidgetElement(root: ParentNode, widgetCode: string): Element | null {
  const code = (widgetCode ?? "").trim();
  if (code.length === 0) return null;

  return root.querySelector("[" + WIDGET_CODE_ATTRIBUTE + '="' + code.replace(/"/g, '\\"') + '"]');
}

/**
 * Brings the element into view and marks it for a short while. The mark is
 * removed again so a page left open does not keep a stale highlight that
 * suggests a citation is still being inspected.
 */
export function applyEvidenceFocus(
  element: Element,
  win: Pick<Window, "setTimeout">,
  holdMs = 4000,
): void {
  if (typeof (element as HTMLElement).scrollIntoView === "function") {
    (element as HTMLElement).scrollIntoView({ behavior: "smooth", block: "center" });
  }

  element.classList.add(EVIDENCE_FOCUS_CLASS);
  win.setTimeout(() => element.classList.remove(EVIDENCE_FOCUS_CLASS), holdMs);
}