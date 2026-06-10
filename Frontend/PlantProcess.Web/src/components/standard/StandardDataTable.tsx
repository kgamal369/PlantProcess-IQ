import type { ReactNode, TableHTMLAttributes } from "react";
import "./standard-components.css";

export const P2T010_TABLE_STANDARDIZATION_MARKER =
  "PPIQ_P2_T010_TABLE_STANDARDIZATION";

export type StandardDataTableAlign = "left" | "right" | "center";
export type StandardDataTableDensity = "compact" | "comfortable";
export type StandardDataTableSortDirection = "asc" | "desc";

export type StandardDataTableColumn<T extends object> = {
  key: string;
  title: ReactNode;
  header?: ReactNode;
  sortable?: boolean;
  align?: StandardDataTableAlign;
  width?: string;
  ariaLabel?: string;
  accessor?: keyof T | ((row: T, index: number) => ReactNode);
  render?: (row: T, index: number) => ReactNode;
};

export type StandardDataTableProps<T extends object> = Omit<
  TableHTMLAttributes<HTMLTableElement>,
  "aria-label"
> & {
  rows?: readonly T[];
  columns?: readonly StandardDataTableColumn<T>[];
  rowKey?: keyof T | ((row: T, index: number) => string | number);
  sortBy?: string;
  sortDirection?: StandardDataTableSortDirection;
  onSort?: (sortBy: string, sortDirection: StandardDataTableSortDirection) => void;
  isLoading?: boolean;
  error?: ReactNode;
  emptyText?: ReactNode;
  loadingText?: ReactNode;
  caption?: ReactNode;
  ariaLabel?: string;
  density?: StandardDataTableDensity;
  getRowClassName?: (row: T, index: number) => string | undefined;
  children?: ReactNode;
};

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

function getRowKey<T extends object>(
  row: T,
  index: number,
  rowKey?: keyof T | ((row: T, index: number) => string | number),
) {
  if (typeof rowKey === "function") return rowKey(row, index);
  if (rowKey) return String(row[rowKey] ?? index);
  return index;
}

function getCellValue<T extends object>(
  row: T,
  column: StandardDataTableColumn<T>,
  index: number,
) {
  if (column.render) return column.render(row, index);
  if (typeof column.accessor === "function") return column.accessor(row, index);
  if (column.accessor) return row[column.accessor] as ReactNode;
  return "";
}

function nextSortDirection(
  columnKey: string,
  sortBy?: string,
  sortDirection?: StandardDataTableSortDirection,
): StandardDataTableSortDirection {
  return sortBy === columnKey && sortDirection === "asc" ? "desc" : "asc";
}

function ariaSort(
  columnKey: string,
  sortable?: boolean,
  sortBy?: string,
  sortDirection?: StandardDataTableSortDirection,
) {
  if (!sortable) return undefined;
  if (sortBy !== columnKey) return "none";
  return sortDirection === "desc" ? "descending" : "ascending";
}

export function StandardDataTable<T extends object>({
  rows = [],
  columns = [],
  rowKey,
  sortBy,
  sortDirection = "asc",
  onSort,
  isLoading = false,
  error,
  emptyText = "No data available.",
  loadingText = "Loading table data...",
  caption,
  ariaLabel,
  density = "comfortable",
  getRowClassName,
  children,
  className,
  ...tableProps
}: StandardDataTableProps<T>) {
  const columnCount = Math.max(columns.length, 1);

  function handleSort(column: StandardDataTableColumn<T>) {
    if (!column.sortable || !onSort) return;
    onSort(column.key, nextSortDirection(column.key, sortBy, sortDirection));
  }

  if (children) {
    return (
      <div
        className={cx(
          "ppiq-std-data-table-shell",
          "ppiq-std-data-table-shell--" + density,
          className,
        )}
        data-ppiq-table-standardization="P2-T010"
      >
        <table
          {...tableProps}
          className="ppiq-std-data-table"
          aria-label={ariaLabel}
          aria-busy={isLoading || undefined}
        >
          {caption ? (
            <caption className="ppiq-std-data-table__caption">{caption}</caption>
          ) : null}
          {children}
        </table>
      </div>
    );
  }

  return (
    <div
      className={cx(
        "ppiq-std-data-table-shell",
        "ppiq-std-data-table-shell--" + density,
        className,
      )}
      data-ppiq-table-standardization="P2-T010"
    >
      <table
        {...tableProps}
        className="ppiq-std-data-table"
        aria-label={ariaLabel ?? "Data table"}
        aria-busy={isLoading || undefined}
      >
        {caption ? (
          <caption className="ppiq-std-data-table__caption">{caption}</caption>
        ) : null}

        <thead>
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                aria-sort={
                  ariaSort(
                    column.key,
                    column.sortable,
                    sortBy,
                    sortDirection,
                  ) as "ascending" | "descending" | "none" | undefined
                }
                className={cx(
                  "ppiq-std-data-table__cell",
                  "ppiq-std-data-table__cell--head",
                  "ppiq-std-data-table__cell--" + (column.align ?? "left"),
                )}
                style={column.width ? { width: column.width } : undefined}
              >
                {column.sortable ? (
                  <button
                    type="button"
                    className="ppiq-std-data-table__sort"
                    onClick={() => handleSort(column)}
                    aria-label={
                      column.ariaLabel ??
                      "Sort by " + String(column.title)
                    }
                  >
                    <span>{column.header ?? column.title}</span>
                    <span aria-hidden="true" className="ppiq-std-data-table__sort-indicator">
                      {sortBy === column.key
                        ? sortDirection === "asc"
                          ? "↑"
                          : "↓"
                        : "↕"}
                    </span>
                  </button>
                ) : (
                  column.header ?? column.title
                )}
              </th>
            ))}
          </tr>
        </thead>

        <tbody>
          {error ? (
            <tr>
              <td
                colSpan={columnCount}
                className="ppiq-std-data-table__state ppiq-std-data-table__state--error"
              >
                {error}
              </td>
            </tr>
          ) : isLoading ? (
            <tr>
              <td
                colSpan={columnCount}
                className="ppiq-std-data-table__state ppiq-std-data-table__state--loading"
              >
                {loadingText}
              </td>
            </tr>
          ) : rows.length === 0 ? (
            <tr>
              <td
                colSpan={columnCount}
                className="ppiq-std-data-table__state ppiq-std-data-table__state--empty"
              >
                {emptyText}
              </td>
            </tr>
          ) : (
            rows.map((row, rowIndex) => (
              <tr
                key={getRowKey(row, rowIndex, rowKey)}
                className={getRowClassName?.(row, rowIndex)}
              >
                {columns.map((column) => (
                  <td
                    key={column.key}
                    className={cx(
                      "ppiq-std-data-table__cell",
                      "ppiq-std-data-table__cell--body",
                      "ppiq-std-data-table__cell--" + (column.align ?? "left"),
                    )}
                  >
                    {getCellValue(row, column, rowIndex)}
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