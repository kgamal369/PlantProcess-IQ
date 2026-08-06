// PPIQ T-037 ACCEPTANCE. The two behavioural clauses, proved without inventing
// an S2 route - which was the explicit ruling: T-037 is the capability, T-038
// is the door.
//
//   CLAUSE 1  assign Axis / Value / Series, persist through writeRoleBinding,
//             re-read, and the mappings are unchanged.
//   CLAUSE 2  a persisted mapping names column X, the next returned-column set
//             no longer contains X, and the warning NAMES X.
//
// The harness below is the persistence seam and nothing more. It holds the
// displayOptionsJson blob that a widget definition already carries, which is
// exactly what WidgetAuthoringPanel holds today and exactly what the S2 shell
// will hold when T-038 wires it. No fixture here is a plant name: these are the
// shapes any returned-column set has.

import { useState } from "react";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { RoleBindingFields } from "./RoleBindingFields";
import { describeStaleBinding, roleLabel } from "./roleBindingPresentation";
import {
  EMPTY_ROLE_BINDING, readRoleBinding, staleRoles, writeRoleBinding,
  type WidgetRoleBinding,
} from "@/api/product-core/widget-role-binding";

const FIRST_RUN = ["group_code", "measured_value", "series_code"];
const SECOND_RUN = ["group_code", "measured_value"];

const BOUND_TO_BOTH = writeRoleBinding("{}", {
  category: "group_code", value: "measured_value", secondary: "series_code",
});

function Harness(props: { columns: readonly string[]; initialJson: string }) {
  const [json, setJson] = useState(props.initialJson);
  const binding = readRoleBinding(json) ?? EMPTY_ROLE_BINDING;
  return (
    <div>
      <RoleBindingFields
        columns={props.columns}
        binding={binding}
        onChange={(next: WidgetRoleBinding) => setJson(writeRoleBinding(json, next))}
      />
      <p data-testid="persisted">{json}</p>
    </div>
  );
}

const persisted = () => screen.getByTestId("persisted").textContent ?? "";

describe("T-037 the shared role-binding capability", () => {
  it("presents the doctrine words and never shows the stored keys", () => {
    render(<Harness columns={FIRST_RUN} initialJson="{}" />);
    expect(roleLabel("category")).toBe("Axis");
    expect(roleLabel("value")).toBe("Value");
    expect(roleLabel("secondary")).toBe("Series");
    expect(screen.getByLabelText("Bind Axis")).toBeInTheDocument();
    expect(screen.getByLabelText("Bind Value")).toBeInTheDocument();
    expect(screen.getByLabelText("Bind Series")).toBeInTheDocument();
    expect(screen.queryByText("secondary")).toBeNull();
  });

  it("offers exactly the columns the query returned and nothing else", () => {
    render(<Harness columns={FIRST_RUN} initialJson="{}" />);
    const axis = screen.getByLabelText("Bind Axis");
    const values = within(axis).getAllByRole("option").map((o) => (o as HTMLOptionElement).value);
    expect(values).toEqual(["", "group_code", "measured_value", "series_code"]);
  });

  it("persists an assignment under the STORED key, not under the word the author saw", async () => {
    render(<Harness columns={FIRST_RUN} initialJson="{}" />);
    await userEvent.selectOptions(screen.getByLabelText("Bind Axis"), "group_code");
    expect(persisted()).toContain("\"category\":\"group_code\"");
    expect(persisted()).not.toContain("Axis");
  });

  it("CLAUSE 1: the mappings survive a re-run of the same query unchanged", async () => {
    const view = render(<Harness columns={FIRST_RUN} initialJson="{}" />);
    await userEvent.selectOptions(screen.getByLabelText("Bind Axis"), "group_code");
    await userEvent.selectOptions(screen.getByLabelText("Bind Value"), "measured_value");

    // The re-run: the same query, the same returned columns, rendered again.
    view.rerender(<Harness columns={FIRST_RUN} initialJson={persisted()} />);

    expect(screen.getByLabelText("Bind Axis")).toHaveValue("group_code");
    expect(screen.getByLabelText("Bind Value")).toHaveValue("measured_value");
    expect(readRoleBinding(persisted())).toEqual({
      category: "group_code", value: "measured_value", secondary: null,
    });
    expect(screen.queryByTestId("role-binding-stale")).toBeNull();
  });

  it("preserves every other key already in the display options blob", async () => {
    render(<Harness columns={FIRST_RUN} initialJson={"{\"legend\":\"right\"}"} />);
    await userEvent.selectOptions(screen.getByLabelText("Bind Axis"), "group_code");
    expect(persisted()).toContain("\"legend\":\"right\"");
    expect(persisted()).toContain("\"category\":\"group_code\"");
  });

  it("CLAUSE 2: a dropped column is reported BY NAME, not as a count", () => {
    render(<Harness columns={SECOND_RUN} initialJson={BOUND_TO_BOTH} />);
    const warning = screen.getByTestId("role-binding-stale");
    expect(warning).toHaveTextContent("series_code");
    expect(warning).toHaveTextContent("Series (series_code)");
  });

  it("CLAUSE 2: the stale role is left unbound rather than silently repointed", () => {
    render(<Harness columns={SECOND_RUN} initialJson={BOUND_TO_BOTH} />);
    expect(screen.getByLabelText("Bind Series")).toHaveValue("");
    expect(screen.getByLabelText("Bind Axis")).toHaveValue("group_code");
    expect(screen.getByTestId("role-binding-stale-secondary")).toBeInTheDocument();
  });

  it("detection and wording agree, and both name the column", () => {
    const binding = readRoleBinding(BOUND_TO_BOTH);
    expect(staleRoles(binding, SECOND_RUN)).toEqual(["secondary"]);
    const sentence = describeStaleBinding(binding, SECOND_RUN);
    expect(sentence).toContain("Series (series_code)");
    expect(sentence).toContain("Nothing has been repointed for you");
  });

  it("says nothing is stale while every mapped column is still returned", () => {
    const binding = readRoleBinding(BOUND_TO_BOTH);
    expect(describeStaleBinding(binding, FIRST_RUN)).toBe("");
    render(<Harness columns={FIRST_RUN} initialJson={BOUND_TO_BOTH} />);
    expect(screen.queryByTestId("role-binding-stale")).toBeNull();
    expect(screen.getByText(/stored by column name/)).toBeInTheDocument();
  });
});