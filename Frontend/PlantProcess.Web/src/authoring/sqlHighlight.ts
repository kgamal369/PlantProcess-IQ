// PPIQ T-036. SYNTAX HIGHLIGHTING, AND NOT ONE THING MORE.
//
// The task asks the SQL editor for syntax highlighting. It also forbids
// building a SQL parser, and Constitution II.7.6 forbids a second
// implementation of a governance rule - SafeSqlValidator on the server is the
// only authority on whether a statement may run.
//
// So this is a DISPLAY CLASSIFIER. It splits text into spans a stylesheet can
// colour: comments, quoted strings, quoted identifiers, numbers, keywords, and
// everything else. It has no grammar, it accepts every input, it rejects
// nothing, and it never reports an opinion about validity.
//
// THE ONE INVARIANT, asserted in both directions by the tests: the spans
// concatenate back to the exact input. A highlighter that alters the text an
// author typed would be far worse than no highlighter at all.

export type SqlSpanKind = "comment" | "string" | "quoted" | "number" | "keyword" | "plain";

export interface SqlSpan {
  text: string;
  kind: SqlSpanKind;
}

// The SQL words this display knows. A missing one shows as plain text,
// which is the harmless direction.
//
// THE FORBIDDEN WORDS ARE HERE TOO, AND THAT IS DELIBERATE. DROP, INSERT and
// the rest are refused by SafeSqlValidator on the server, but leaving them
// uncoloured would make the highlighter a covert validity signal - an author
// would read "not highlighted" as "not recognised" and be told something
// about legality by a component that has no business saying it. A forbidden
// statement must look exactly like a permitted one until the server rules.
const KEYWORDS = [
  "SELECT", "FROM", "WHERE", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER",
  "ON", "AND", "OR", "NOT", "NULL", "IS", "IN", "AS", "WITH", "DISTINCT",
  "ORDER", "GROUP", "BY", "HAVING", "LIMIT", "OFFSET", "UNION", "ALL",
  "CASE", "WHEN", "THEN", "ELSE", "END", "CAST", "ASC", "DESC", "BETWEEN", "LIKE",
  "DROP", "CREATE", "ALTER", "TRUNCATE", "INSERT", "UPDATE", "DELETE",
  "INTO", "VALUES", "SET", "TABLE", "VIEW", "COPY", "GRANT", "REVOKE",
];

const KEYWORD_SET = new Set(KEYWORDS);

// Ordered alternation. Comments and strings come first, so a keyword written
// inside either of them stays inside it - which is exactly the case a naive
// word-by-word highlighter gets wrong.
const SCANNER = new RegExp(
  [
    "--[^\\n]*",           // line comment
    "/\\*[\\s\\S]*?\\*/",  // block comment
    "'(?:''|[^'])*'",      // string literal, '' escape included
    '"(?:""|[^"])*"',      // quoted identifier
    "\\d+(?:\\.\\d+)?",    // number
    "[A-Za-z_][A-Za-z0-9_]*", // word
  ].join("|"),
  "g",
);

/**
 * The input, split into spans that concatenate back to it exactly.
 *
 * Every character of the input appears in exactly one span, including
 * whitespace, punctuation and anything the scanner does not recognise, which
 * is carried through as "plain".
 */
export function highlightSpans(sql: string): SqlSpan[] {
  const text = sql ?? "";
  const spans: SqlSpan[] = [];
  let at = 0;

  const push = (value: string, kind: SqlSpanKind) => {
    if (value !== "") { spans.push({ text: value, kind }); }
  };

  SCANNER.lastIndex = 0;
  let match = SCANNER.exec(text);
  while (match !== null) {
    push(text.slice(at, match.index), "plain");
    const token = match[0];
    if (token.indexOf("--") === 0 || token.indexOf("/*") === 0) {
      push(token, "comment");
    } else if (token.charAt(0) === "'") {
      push(token, "string");
    } else if (token.charAt(0) === '"') {
      push(token, "quoted");
    } else if (/^\d/.test(token)) {
      push(token, "number");
    } else {
      // WORD BOUNDARIES MATTER. "created_at" is one word and is not a keyword,
      // which is the same boundary discipline SafeSqlValidator applies on the
      // server - and the reason a column called created_at stays legal.
      push(token, KEYWORD_SET.has(token.toUpperCase()) ? "keyword" : "plain");
    }
    at = match.index + token.length;
    match = SCANNER.exec(text);
  }
  push(text.slice(at), "plain");
  return spans;
}
