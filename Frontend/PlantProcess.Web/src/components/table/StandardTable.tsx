import type { ReactNode } from "react";
import { DataFetchBoundary } from "@/components/hardening/DataFetchBoundary";

export type StandardTableColumn<TRow> = {
  key: string;
  header: ReactNode;
  width?: string;
  align?: "left" | "center" | "right";
  render: (row: TRow, rowIndex: number) => ReactNode;
};

export type StandardTableProps<TRow> = {
  title?: string;
  description?: string;
  rows: TRow[];
  columns: StandardTableColumn<TRow>[];
  rowKey: (row: TRow, rowIndex: number) => string;
  isLoading?: boolean;
  error?: unknown;
  emptyMessage?: string;
  onRetry?: () => void;
  density?: "compact" | "normal";
};

export function StandardTable<TRow>({
  title,
  description,
  rows,
  columns,
  rowKey,
  isLoading = false,
  error,
  emptyMessage = "No rows available.",
  onRetry,
  density = "normal",
}: StandardTableProps<TRow>) {
  return (
    <section className={`standard-table-shell standard-table-shell--${density}`}>
      {title || description ? (
        <header className="standard-table-header">
          <div>
            {title ? <h3>{title}</h3> : null}
            {description ? <p>{description}</p> : null}
          </div>
        </header>
      ) : null}

      <DataFetchBoundary
        title={title ?? "Table"}
        isLoading={isLoading}
        error={error}
        isEmpty={!isLoading && !error && rows.length === 0}
        emptyMessage={emptyMessage}
        onRetry={onRetry}
      >
        <div className="standard-table-scroll">
          <table className="standard-table">
            <thead>
              <tr>
                {columns.map((column) => (
                  <th
                    key={column.key}
                    style={{ width: column.width }}
                    className={`align-${column.align ?? "left"}`}
                  >
                    {column.header}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody>
              {rows.map((row, rowIndex) => (
                <tr key={rowKey(row, rowIndex)}>
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={`align-${column.align ?? "left"}`}
                    >
                      {column.render(row, rowIndex)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </DataFetchBoundary>
    </section>
  );
}

export default StandardTable;