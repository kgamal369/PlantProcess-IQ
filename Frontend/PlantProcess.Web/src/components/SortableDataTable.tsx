import type { ReactNode } from "react";
import type { SortDirection } from "../api/plantProcessApi";

export interface SortableColumn<T> {
  key: string;
  title: string;
  sortable?: boolean;
  align?: "left" | "right" | "center";
  render: (row: T) => ReactNode;
}

export function SortableDataTable<T>({
  rows,
  columns,
  sortBy,
  sortDirection,
  onSort,
  emptyText = "No data available.",
}: {
  rows: T[];
  columns: SortableColumn<T>[];
  sortBy?: string;
  sortDirection?: SortDirection;
  onSort?: (sortBy: string, sortDirection: SortDirection) => void;
  emptyText?: string;
}) {
  function handleSort(column: SortableColumn<T>) {
    if (!column.sortable || !onSort) return;

    const nextDirection: SortDirection =
      sortBy === column.key && sortDirection === "asc" ? "desc" : "asc";

    onSort(column.key, nextDirection);
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                style={{ textAlign: column.align ?? "left" }}
                className={column.sortable ? "sortable-header" : undefined}
                onClick={() => handleSort(column)}
              >
                {column.title}
                {column.sortable && sortBy === column.key ? (
                  <span className="sort-indicator">
                    {sortDirection === "asc" ? " ↑" : " ↓"}
                  </span>
                ) : null}
              </th>
            ))}
          </tr>
        </thead>

        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={columns.length}>{emptyText}</td>
            </tr>
          ) : (
            rows.map((row, index) => (
              <tr key={index}>
                {columns.map((column) => (
                  <td
                    key={column.key}
                    style={{ textAlign: column.align ?? "left" }}
                  >
                    {column.render(row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}