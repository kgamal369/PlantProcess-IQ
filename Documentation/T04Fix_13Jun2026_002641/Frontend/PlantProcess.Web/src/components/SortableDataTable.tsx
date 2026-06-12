import type { ReactNode } from "react";
import {
  StandardDataTable,
  type StandardDataTableAlign,
  type StandardDataTableColumn,
  type StandardDataTableSortDirection,
} from "@/components/standard";

export const P2T010_SORTABLE_TABLE_STANDARDIZATION_MARKER =
  "PPIQ_P2_T010_TABLE_STANDARDIZATION";

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

  return (
    <StandardDataTable
      rows={rows}
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
}