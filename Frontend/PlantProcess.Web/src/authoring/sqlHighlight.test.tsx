// PPIQ T-036. The highlighter under test.
//
// The invariant first: whatever it does to the appearance, it must not alter
// the text. Everything else is colouring.

import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { highlightSpans } from "./sqlHighlight";
import { SqlHighlighted } from "./SqlHighlighted";

function rejoin(sql: string): string {
  return highlightSpans(sql).map((s) => s.text).join("");
}

describe("the invariant", () => {
  it("gives back exactly what it was given", () => {
    const samples = [
      "",
      "SELECT 1",
      'SELECT t0."alpha_key"\n  FROM "staging_one"."alpha" t0\n WHERE t0."widget_mass" > 12.5',
      "-- a comment with SELECT inside it\nSELECT 1",
      "/* block\n   comment */ SELECT 'text with '' quote and FROM inside'",
      "SELECT   \t  *\n\n\nFROM x   ",
      "SELECT a::text, b[1], (c + d) * 2 -- trailing",
    ];
    for (const sample of samples) {
      expect(rejoin(sample)).toBe(sample);
    }
  });

  it("covers every character exactly once", () => {
    const sql = "SELECT a FROM b WHERE c = 'x' -- note";
    const spans = highlightSpans(sql);
    expect(spans.map((s) => s.text).join("").length).toBe(sql.length);
    expect(spans.every((s) => s.text !== "")).toBe(true);
  });
});

describe("what it distinguishes", () => {
  function kindOf(sql: string, text: string) {
    return highlightSpans(sql).filter((s) => s.text === text).map((s) => s.kind);
  }

  it("marks keywords whatever their case", () => {
    expect(kindOf("select 1", "select")).toEqual(["keyword"]);
    expect(kindOf("SELECT 1", "SELECT")).toEqual(["keyword"]);
  });

  it("leaves a keyword inside a string or a comment alone", () => {
    expect(kindOf("SELECT 'FROM'", "'FROM'")).toEqual(["string"]);
    expect(kindOf("-- FROM here\nSELECT 1", "-- FROM here")).toEqual(["comment"]);
  });

  it("does not split a word that merely contains a keyword", () => {
    // created_at stays legal on the server for exactly this reason, and the
    // display must not suggest otherwise.
    expect(kindOf("SELECT created_at", "created_at")).toEqual(["plain"]);
    expect(kindOf("SELECT selected_by", "selected_by")).toEqual(["plain"]);
  });

  it("marks quoted identifiers, numbers and strings apart", () => {
    expect(kindOf('SELECT "col" FROM t', '"col"')).toEqual(["quoted"]);
    expect(kindOf("SELECT 12.5", "12.5")).toEqual(["number"]);
    expect(kindOf("SELECT 'a'", "'a'")).toEqual(["string"]);
  });

  it("says nothing about whether the statement is allowed to run", () => {
    // A forbidden statement highlights exactly like a permitted one. The
    // server decides; this only colours.
    const spans = highlightSpans("DROP TABLE x");
    expect(spans.map((s) => s.text).join("")).toBe("DROP TABLE x");
    expect(spans.some((s) => s.kind === "keyword")).toBe(true);
  });
});

describe("the rendering", () => {
  it("shows the statement unchanged and marks its syntax", () => {
    render(<SqlHighlighted sql="SELECT created_at FROM t" testId="hl" />);
    const node = screen.getByTestId("hl");
    expect(node.textContent).toBe("SELECT created_at FROM t");
    expect(node.querySelectorAll(".sql-hl__keyword").length).toBe(2);
    expect(node.querySelectorAll(".sql-hl__plain").length).toBeGreaterThan(0);
  });

  it("hides the copy that sits behind the editor from assistive technology", () => {
    render(<SqlHighlighted sql="SELECT 1" testId="ghost" ariaHidden />);
    expect(screen.getByTestId("ghost")).toHaveAttribute("aria-hidden", "true");
  });
});
