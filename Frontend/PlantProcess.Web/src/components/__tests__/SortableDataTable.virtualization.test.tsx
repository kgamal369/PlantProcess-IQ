import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SortableDataTable, type SortableColumn } from "@/components/SortableDataTable";

type Row = {
  id: number;
  name: string;
};

const columns: SortableColumn<Row>[] = [
  {
    key: "id",
    title: "ID",
    render: (row) => row.id,
  },
  {
    key: "name",
    title: "Name",
    render: (row) => row.name,
  },
];

describe("SortableDataTable virtualization", () => {
  it("renders empty state", () => {
    render(<SortableDataTable rows={[]} columns={columns} emptyText="No rows." />);

    expect(screen.getByText("No rows.")).toBeInTheDocument();
  });

  it("renders small table normally", () => {
    render(
      <SortableDataTable
        rows={[{ id: 1, name: "One" }]}
        columns={columns}
        getRowKey={(row) => row.id}
      />
    );

    expect(screen.getByText("One")).toBeInTheDocument();
  });

  it("enables virtualization for large tables", () => {
    const rows = Array.from({ length: 5000 }, (_, index) => ({
      id: index + 1,
      name: `Row ${index + 1}`,
    }));

    render(
      <SortableDataTable
        rows={rows}
        columns={columns}
        getRowKey={(row) => row.id}
      />
    );

    expect(screen.getByText("5,000 rows")).toBeInTheDocument();
    expect(screen.getByText("Virtualized rendering enabled")).toBeInTheDocument();
  });
});