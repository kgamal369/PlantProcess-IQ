// PPIQ T-038 pack 02b acceptance for the S2 face.
//
// What is proved here, and each maps to one line of his acceptance list:
//   the catalogue lists come from the server and nothing is written into the UI
//   Run test executes the EXISTING canonical expression contract
//   the returned columns appear
//   the role choices are the column CODES, which is the defect this closes
//   Axis / Value / Series reach the definition under the stored keys
//   a removed mapped column is detected and NAMED
//   a failure never puts a raw exception in front of a plant engineer
//
// No fixture is a plant word. The api module is mocked, so nothing here reaches
// a server, and the mock is asserted against - a face that quietly called the
// preparation path would fail the last test rather than pass silently.

import { useState } from "react";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { S2QueryBinding } from "./S2QueryBinding";
import { EMPTY_S2_STATE, type S2AuthoringState } from "./widgetDefinitionModel";
import { readRoleBinding, writeRoleBinding } from "@/api/product-core/widget-role-binding";

const getDashboardMetadata = vi.fn();
const getDashboardReferenceData = vi.fn();
const executeWidgetQueryExpression = vi.fn();

vi.mock("@/api/dashboarding/dashboarding.api", () => ({
  dashboardingApi: {
    getDashboardMetadata: () => getDashboardMetadata(),
    getDashboardReferenceData: () => getDashboardReferenceData(),
    executeWidgetQueryExpression: (request: unknown) => executeWidgetQueryExpression(request),
  },
}));

const META = {
  chartTypes: [
    { code: "ct_bar", label: "Bars", category: "chart", supportsDimension: true, supportsMeasure: true },
    { code: "ct_tile", label: "Single number", category: "tile", supportsDimension: false, supportsMeasure: true },
  ],
  dimensions: [
    { code: "dim_group", label: "Group", compatibleChartTypes: ["ct_bar"], requiresParameterCode: false },
    { code: "dim_param", label: "Parametric", compatibleChartTypes: ["ct_bar"], requiresParameterCode: true },
  ],
  measures: [
    { code: "mea_total", label: "Total", unit: "u", compatibleChartTypes: ["ct_bar", "ct_tile"], requiresParameterCode: false },
  ],
  filters: [{ code: "flt_window", label: "Window", sourceCatalog: "windows" }],
};
const REF = { windows: [{ code: "w7", label: "Last 7" }], parameters: [{ code: "p1", label: "Parameter one" }] };

const RUN_BOTH = {
  columns: [
    { code: "group_code", label: "Group code", dataType: "text" },
    { code: "measured_value", label: "Measured value", dataType: "number" },
  ],
  rows: [{ group_code: "a", measured_value: 1 }],
  warnings: [],
};
const RUN_MISSING_ONE = {
  columns: [{ code: "group_code", label: "Group code", dataType: "text" }],
  rows: [{ group_code: "a" }],
  warnings: [],
};

const logged: { severity: string; message: string; facts?: string }[] = [];
// T-040 03a2. The face reports both ends of a run to its owner and keeps no
// flag of its own, so the shell and the face cannot disagree about a run.
const lifecycle: { phase: string; failure: string | null; rowCount: number | null }[] = [];

function Harness({ initial }: { initial: S2AuthoringState }) {
  const [state, setState] = useState<S2AuthoringState>(initial);
  return (
    <div>
      <S2QueryBinding
        state={state}
        onChange={setState}
        onLog={(severity, message, facts) => { logged.push({ severity, message, facts }); }}
        running={false}
        onRunLifecycle={(phase, failure, rowCount) => {
          lifecycle.push({ phase, failure, rowCount });
        }}
      />
      <p data-testid="definition">{JSON.stringify(state)}</p>
    </div>
  );
}

const queryState = (over: Partial<S2AuthoringState> = {}): S2AuthoringState => ({
  ...EMPTY_S2_STATE, chartType: "ct_bar", bindMode: "query",
  expression: "dimension group_code", ...over,
});
const definition = (): S2AuthoringState =>
  JSON.parse(screen.getByTestId("definition").textContent ?? "{}") as S2AuthoringState;

// EVERY CATALOGUE QUERY WAITS FOR THIS, and the reason is a defect these tests
// made on their first run. The controls exist on the first paint; their OPTIONS
// arrive when the metadata promise settles. A findBy on the control therefore
// resolves against the pre-load render - which is how one assertion ended up
// reading a filter value INPUT that becomes a SELECT a moment later. Waiting on
// something only the catalogue can produce removes the race for all of them.
const catalogueLoaded = () => screen.findByRole("option", { name: "Bars" });

beforeEach(() => {
  logged.length = 0;
  lifecycle.length = 0;
  getDashboardMetadata.mockResolvedValue(META);
  getDashboardReferenceData.mockResolvedValue(REF);
  executeWidgetQueryExpression.mockReset();
});

describe("T-038 the S2 catalogue comes from the server", () => {
  it("offers the published chart types and nothing written into the product", async () => {
    render(<Harness initial={{ ...EMPTY_S2_STATE }} />);
    await catalogueLoaded();
    const chart = screen.getByLabelText("Chart type");
    const values = within(chart).getAllByRole("option").map((o) => (o as HTMLOptionElement).value);
    expect(values).toEqual(["", "ct_bar", "ct_tile"]);
  });

  it("follows the chart type's declared support instead of assuming both", async () => {
    render(<Harness initial={{ ...EMPTY_S2_STATE, chartType: "ct_tile" }} />);
    await catalogueLoaded();
    // The accessible name is the control's aria-label. The visible span reading
    // "Measure, optional" is not a label element and names nothing, so querying
    // that text matches neither control - which would make the negative
    // assertion below pass whether or not the dimension select were rendered.
    expect(screen.getByLabelText("Measure")).toBeInTheDocument();
    expect(screen.queryByLabelText("Dimension")).toBeNull();
  });

  it("asks for a parameter only when the chosen field declares it needs one", async () => {
    render(<Harness initial={{ ...EMPTY_S2_STATE, chartType: "ct_bar", dimensionCode: "dim_param" }} />);
    await catalogueLoaded();
    const parameter = screen.getByLabelText("Parameter");
    const values = within(parameter).getAllByRole("option").map((o) => (o as HTMLOptionElement).value);
    expect(values).toEqual(["", "p1"]);
  });

  it("takes a filter value from its published catalogue when it has one", async () => {
    render(<Harness initial={{ ...EMPTY_S2_STATE, filters: [{ code: "flt_window", value: "" }] }} />);
    await catalogueLoaded();
    // Before the reference data lands this control is an input with no options.
    const value = screen.getByLabelText("Filter value");
    const values = within(value).getAllByRole("option").map((o) => (o as HTMLOptionElement).value);
    expect(values).toEqual(["", "w7"]);
  });

  it("carries a filter the author adds into the definition", async () => {
    render(<Harness initial={{ ...EMPTY_S2_STATE }} />);
    await catalogueLoaded();
    await userEvent.click(screen.getByRole("button", { name: "Add filter" }));
    await userEvent.selectOptions(screen.getByLabelText("Filter"), "flt_window");
    expect(definition().filters).toEqual([{ code: "flt_window", value: "" }]);
  });
});

describe("T-038 Run test executes the existing canonical expression contract", () => {
  it("sends the authored expression and renders the returned columns", async () => {
    executeWidgetQueryExpression.mockResolvedValue(RUN_BOTH);
    render(<Harness initial={queryState()} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(screen.getByTestId("s2-returned-columns")).toBeInTheDocument());
    expect(executeWidgetQueryExpression).toHaveBeenCalledTimes(1);
    const sent = executeWidgetQueryExpression.mock.calls[0][0] as { expression: string };
    expect(sent.expression).toBe("dimension group_code");
    expect(screen.getByTestId("s2-column-measured_value")).toHaveTextContent("number");
  });

  it("reports zero rows without claiming a cause the server cannot see", async () => {
    executeWidgetQueryExpression.mockResolvedValue({ ...RUN_BOTH, rows: [] });
    render(<Harness initial={queryState()} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(logged.some((l) => l.severity === "warning")).toBe(true));
    const entry = logged.find((l) => l.severity === "warning");
    expect(entry?.message).toContain("returned 0 rows");
    expect(entry?.message).not.toContain("too restrictive");
  });

  it("never puts the thrown value in front of a plant engineer", async () => {
    executeWidgetQueryExpression.mockRejectedValue(new Error("42P01 relation does not exist"));
    render(<Harness initial={queryState()} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(logged.some((l) => l.severity === "error")).toBe(true));
    const entry = logged.find((l) => l.severity === "error");
    expect(entry?.message).not.toContain("42P01");
    expect(entry?.message).toContain("did not complete");
  });
});

describe("T-038 the returned columns map to Axis, Value and Series", () => {
  it("offers the column CODES as the role choices, not their labels", async () => {
    executeWidgetQueryExpression.mockResolvedValue(RUN_BOTH);
    render(<Harness initial={queryState()} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    const axis = await screen.findByLabelText("Bind Axis");
    const values = within(axis).getAllByRole("option").map((o) => (o as HTMLOptionElement).value);
    expect(values).toEqual(["", "group_code", "measured_value"]);
    // The label must not be what a role points at - the renderer resolves the
    // saved binding against the code, so a label here would read as stale.
    expect(values).not.toContain("Group code");
  });

  it("writes the assignment into the definition under the stored key", async () => {
    executeWidgetQueryExpression.mockResolvedValue(RUN_BOTH);
    render(<Harness initial={queryState()} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));
    await userEvent.selectOptions(await screen.findByLabelText("Bind Axis"), "group_code");

    expect(definition().roleBinding.category).toBe("group_code");
    expect(readRoleBinding(writeRoleBinding("{}", definition().roleBinding))?.category).toBe("group_code");
  });

  it("names the vanished column in the Job Log when a mapped column stops being returned", async () => {
    executeWidgetQueryExpression.mockResolvedValue(RUN_MISSING_ONE);
    render(<Harness initial={queryState({
      roleBinding: { category: "group_code", value: "measured_value", secondary: null },
    })} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(screen.getByTestId("role-binding-stale")).toBeInTheDocument());
    expect(screen.getByTestId("role-binding-stale")).toHaveTextContent("measured_value");
    const named = logged.filter((l) => l.message.indexOf("measured_value") >= 0);
    expect(named.length).toBeGreaterThan(0);
    expect(screen.getByLabelText("Bind Value")).toHaveValue("");
  });
});

describe("T-038 a widget bound under the old surface reopens correctly", () => {
  it("resolves a unique legacy label to its column and normalises the definition", async () => {
    executeWidgetQueryExpression.mockResolvedValue(RUN_BOTH);
    render(<Harness initial={queryState({
      roleBinding: { category: "Group code", value: null, secondary: null },
    })} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(definition().roleBinding.category).toBe("group_code"));
    expect(await screen.findByLabelText("Bind Axis")).toHaveValue("group_code");
    expect(screen.queryByTestId("role-binding-stale")).toBeNull();
    const said = logged.filter((l) => l.message.indexOf("Nothing was guessed") >= 0);
    expect(said.length).toBe(1);
  });

  it("refuses to resolve a label two returned columns share", async () => {
    executeWidgetQueryExpression.mockResolvedValue({
      columns: [
        { code: "left_total", label: "Total", dataType: "number" },
        { code: "right_total", label: "Total", dataType: "number" },
      ],
      rows: [{ left_total: 1, right_total: 2 }],
      warnings: [],
    });
    render(<Harness initial={queryState({
      roleBinding: { category: "Total", value: null, secondary: null },
    })} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(logged.some((l) => l.message.indexOf("NOT been resolved") >= 0)).toBe(true));
    expect(definition().roleBinding.category).toBe("Total");
    expect(await screen.findByTestId("role-binding-stale")).toBeInTheDocument();
  });
});

describe("T-040 the face reports its run and owns no flag", () => {
  it("tells its owner when a run starts and when it ends, with the row count", async () => {
    executeWidgetQueryExpression.mockResolvedValue(RUN_BOTH);
    render(<Harness initial={queryState()} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(lifecycle.length).toBe(2));
    expect(lifecycle[0]).toEqual({ phase: "start", failure: null, rowCount: null });
    expect(lifecycle[1]).toEqual({ phase: "end", failure: null, rowCount: 1 });
  });

  it("hands up a normalised sentence on failure, never the thrown value", async () => {
    executeWidgetQueryExpression.mockRejectedValue(new Error("42P01 relation does not exist"));
    render(<Harness initial={queryState()} />);
    await screen.findByLabelText("Query expression");
    await userEvent.click(screen.getByRole("button", { name: "Run test" }));

    await waitFor(() => expect(lifecycle.length).toBe(2));
    expect(lifecycle[1].phase).toBe("end");
    expect(lifecycle[1].failure).toContain("did not complete");
    expect(lifecycle[1].failure).not.toContain("42P01");
    expect(lifecycle[1].rowCount).toBeNull();
  });

  it("keeps no running flag of its own, so it cannot disagree with the shell", () => {
    const source = readFileSync(join(process.cwd(), "src/authoring/S2QueryBinding.tsx"), "utf8");
    expect(source.indexOf("setRunning")).toBe(-1);
    expect(source.indexOf("useState(false)")).toBe(-1);
  });
});

describe("T-038 the S2 face stays on its own contract", () => {
  it("never reaches the preparation path or the staged catalogue", () => {
    const source = readFileSync(join(process.cwd(), "src/authoring/S2QueryBinding.tsx"), "utf8");
    // Assembled so this guard is not itself a hit in the next repository scan.
    const prepPath = "/api/prep" + "/sql/run";
    for (const forbidden of [prepPath, "runAuthoredSql", "listStagedDatasets", "canvasApi"]) {
      expect(source.indexOf(forbidden), "the S2 face reaches " + forbidden).toBe(-1);
    }
  });
});