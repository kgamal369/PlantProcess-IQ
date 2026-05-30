
import { describe, expect, it } from "vitest";
import { renderToString } from "react-dom/server";
import { StandardTable, type StandardTableColumn } from "../StandardTable";

type Row = { id: string; name: string; score: number };

const rows: Row[] = [
  { id: "1", name: "A", score: 10 },
  { id: "2", name: "B", score: 20 },
];

const columns: StandardTableColumn<Row>[] = [
  { key: "name", header: "Name", accessor: "name", sortable: true },
  { key: "score", header: "Score", accessor: "score", align: "right" },
];

describe("StandardTable", () => {
  it("renders populated table", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={rows} getRowKey={(row) => row.id} />,
    );

    expect(html).toContain("role=\"table\"");
    expect(html).toContain("Name");
    expect(html).toContain("Score");
  });

  it("renders empty state", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} />,
    );

    expect(html).toContain("No records available");
  });

  it("renders loading state", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} isLoading />,
    );

    expect(html).toContain("ppiq-std-table-skeleton");
  });

  it("renders error state", () => {
    const html = renderToString(
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} hasError />,
    );

    expect(html).toContain("Refreshing table");
  });
});
