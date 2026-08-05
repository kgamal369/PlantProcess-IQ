// PPIQ T-036. The highlighted rendering of a statement.
//
// Two uses, one component: the read-only compiled query, and a copy sitting
// under the transparent textarea so the editor appears highlighted while the
// textarea alone still holds the text.
//
// It renders spans and nothing else - no editing, no validation, no opinion.

import { highlightSpans } from "./sqlHighlight";

export interface SqlHighlightedProps {
  sql: string;
  className?: string;
  testId?: string;
  /** True for the copy behind the editor, which no screen reader should read. */
  ariaHidden?: boolean;
}

export function SqlHighlighted({ sql, className, testId, ariaHidden }: SqlHighlightedProps) {
  const spans = highlightSpans(sql);
  return (
    <pre
      className={"sql-hl" + (className ? " " + className : "")}
      data-testid={testId}
      aria-hidden={ariaHidden ? true : undefined}
    >
      {spans.map((s, i) => (
        <span key={i} className={"sql-hl__" + s.kind}>{s.text}</span>
      ))}
    </pre>
  );
}
