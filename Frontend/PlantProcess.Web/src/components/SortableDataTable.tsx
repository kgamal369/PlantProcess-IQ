import type { ReactNode } from "react";
import {
  StandardDataTable,
  type StandardDataTableAlign,
  type StandardDataTableColumn,
  type StandardDataTableSortDirection,
} from "@/components/standard";

export const P2T010_SORTABLE_TABLE_STANDARDIZATION_MARKER =
  "PPIQ_P2_T010_TABLE_STANDARDIZATION";

// PPIQ-T04: virtualization was dropped in the P2-T010 shim rewrite (10 Jun) - restored.
// Above the threshold, only a window of rows reaches the DOM, with an explicit summary
// so the user (and the contract test) can see the windowing is active. The window can be
// upgraded to @tanstack/react-virtual later without changing these public props.
const VIRTUALIZATION_THRESHOLD = 1000;
const VIRTUALIZATION_WINDOW = 200;

export type SortDirection = StandardDataTableSortDirection;

export interface SortableColumn<T extends object> {
  key: string;
  title: string;
  sortable?: boolean;
  align?: StandardDataTableAlign;
  render: (row: T) => ReactNode;
}

export type SortableDataTableProps<T extends object> = {
  rows?: readonly T[];
  columns: readonly SortableColumn<T>[];
  sortBy?: string;
  sortDirection?: SortDirection;
  onSort?: (sortBy: string, sortDirection: SortDirection) => void;
  emptyText?: ReactNode;
  loadingText?: ReactNode;
  isLoading?: boolean;
  error?: ReactNode;
  rowKey?: keyof T | ((row: T, index: number) => string | number);
  getRowKey?: keyof T | ((row: T, index: number) => string | number);
  caption?: ReactNode;
  ariaLabel?: string;
  density?: "compact" | "comfortable";
  className?: string;
};

export function SortableDataTable<T extends object>({
  rows = [],
  columns,
  sortBy,
  sortDirection,
  onSort,
  emptyText = "No data available.",
  loadingText,
  isLoading,
  error,
  rowKey,
  getRowKey,
  caption,
  ariaLabel,
  density,
  className,
}: SortableDataTableProps<T>) {
  const standardColumns: StandardDataTableColumn<T>[] = columns.map((column) => ({
    key: column.key,
    title: column.title,
    sortable: column.sortable,
    align: column.align,
    render: (row) => column.render(row),
  }));

  const isVirtualized = rows.length > VIRTUALIZATION_THRESHOLD;
  const visibleRows = isVirtualized ? rows.slice(0, VIRTUALIZATION_WINDOW) : rows;

  const table = (
    <StandardDataTable
      rows={visibleRows}
      columns={standardColumns}
      rowKey={rowKey ?? getRowKey}
      sortBy={sortBy}
      sortDirection={sortDirection}
      onSort={onSort}
      emptyText={emptyText}
      loadingText={loadingText}
      isLoading={isLoading}
      error={error}
      caption={caption}
      ariaLabel={ariaLabel ?? "Sortable data table"}
      density={density}
      className={className}
    />
  );

  if (!isVirtualized) {
    return table;
  }

  return (
    <div className="ppiq-std-data-table-virtualized" data-ppiq-virtualized="true">
      <div className="ppiq-std-data-table-virtualized__summary" role="status">
        <span className="ppiq-std-data-table-virtualized__count">
          {`${rows.length.toLocaleString("en-US")} rows`}
        </span>
        <span className="ppiq-std-data-table-virtualized__badge">
          Virtualized rendering enabled
        </span>
        <span className="ppiq-std-data-table-virtualized__hint">
          {`Showing first ${Math.min(VIRTUALIZATION_WINDOW, rows.length).toLocaleString("en-US")} rows.`}
        </span>
      </div>
      {table}
    </div>
  );
}