import { describe, expect, it } from "vitest";

import { describeThrownAction, describeThrownPreview } from "./previewReport";

// T-046. THE AUTHOR MUST SEE WHY THE SERVER SAID NO.
//
// Before this, every failure - including every deliberate 4xx refusal the chart
// grammar produces - reached the author as "check that the API is running".
// That is a false diagnosis, and a false diagnosis sends a plant engineer to
// restart a service when their widget is what needs changing.

const transport = "That action did not complete, because the request to the server did not return."
  + " Check that the API is running, then try it again.";

function apiError(status: number, body: unknown) {
  return {
    name: "ApiError",
    status,
    responseText: typeof body === "string" ? body : JSON.stringify(body),
    path: "/analytics/dashboard/widgets/query",
    method: "POST",
  };
}

describe("describeThrownAction", () => {
  it("shows the sentence a business-rule refusal carried", () => {
    const thrown = apiError(422, {
      title: "business_rule.violation",
      status: 422,
      detail: "chart_not_supported_for_this_result: This grouping produces one category,"
        + " so the chart would be a single slice at one hundred percent."
        + " Choose a dimension with more than one value.",
    });

    const sentence = describeThrownAction(thrown);

    expect(sentence).toContain("one category");
    expect(sentence).toContain("Choose a dimension with more than one value.");
    expect(sentence).not.toBe(transport);
  });

  it("strips the machine rule code from the front of the sentence", () => {
    const thrown = apiError(422, { detail: "aggregate_population_limit_exceeded: This aggregate was not computed." });

    expect(describeThrownAction(thrown)).toBe("This aggregate was not computed.");
  });

  it("keeps prose that merely contains a colon", () => {
    const thrown = apiError(400, { detail: "Two problems were found: the first and the second." });

    expect(describeThrownAction(thrown)).toBe("Two problems were found: the first and the second.");
  });

  it("shows every validation message, not the first", () => {
    const thrown = apiError(400, {
      title: "validation.failed",
      errors: {
        MeasureCode: ["Unsupported measure code 'nope'."],
        DimensionCode: ["Dimension code is required for this chart type."],
      },
    });

    const sentence = describeThrownAction(thrown);

    expect(sentence).toContain("Unsupported measure code 'nope'.");
    expect(sentence).toContain("Dimension code is required for this chart type.");
  });

  // Everything below is a failure the author cannot act on. The transport
  // sentence is the honest answer, and a raw exception string never is.
  it("keeps the transport sentence for a server fault", () => {
    expect(describeThrownAction(apiError(500, { detail: "Object reference not set." }))).toBe(transport);
  });

  it("keeps the transport sentence when the request never returned", () => {
    expect(describeThrownAction(new Error("Failed to fetch"))).toBe(transport);
    expect(describeThrownAction(apiError(0, ""))).toBe(transport);
  });

  it("keeps the transport sentence when a 4xx body is not the problem contract", () => {
    expect(describeThrownAction(apiError(404, "<html>Not Found</html>"))).toBe(transport);
  });

  it("never leaks a thrown value that carries no server sentence", () => {
    const nasty = { status: 400, responseText: "{}", stack: "at Object.<anonymous> (secret.ts:1:1)" };

    expect(describeThrownAction(nasty)).toBe(transport);
    expect(describeThrownAction(nasty)).not.toContain("secret.ts");
  });

  it("leaves the preview sentence alone - a preview reports its own outcome", () => {
    expect(describeThrownPreview(apiError(422, { detail: "x: y" })))
      .toContain("The preview did not complete");
  });
});