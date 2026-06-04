
import { useMemo, useState, type ReactNode } from "react";
import { ChevronDown, ChevronUp, Download, RefreshCw, Search } from "lucide-react";
import { StandardButton } from "./StandardButton";
import { StandardInput, StandardSelect } from "./StandardFields";
import "./standard-components.css";

export type StandardTableDensity = "compact" | "comfortable" | "spacious";
export type StandardTableSortDirection = "asc" | "desc";
export type StandardTableSelectionMode = "none" | "single" | "multi";

export type StandardTableColumn<T> = {
  key: string;
  header: ReactNode;
  accessor?: keyof T | ((row: T) => unknown);
  cell?: (row: T, rowIndex: number) => ReactNode;
  sortable?: boolean;
  filterable?: boolean;
  width?: number;
  minWidth?: number;
  align?: "left" | "center" | "right";
  hidden?: boolean;
};

export type StandardTableQuery = {
  pageIndex: number;
  pageSize: number;
  filter: string;
  sorting: Array<{ key: string; direction: StandardTableSortDirection }>;
};

export type StandardTableProps<T> = {
  columns: ReadonlyArray<StandardTableColumn<T>>;
  data: ReadonlyArray<T>;
  getRowKey: (row: T, rowIndex: number) => string;
  caption?: string;
  isLoading?: boolean;
  hasError?: boolean;
  errorMessage?: ReactNode;
  onRetry?: () => void;
  emptyTitle?: ReactNode;
  emptyDescription?: ReactNode;
  primaryAction?: ReactNode;
  selectionMode?: StandardTableSelectionMode;
  selectedRowKeys?: ReadonlyArray<string>;
  onSelectionChange?: (keys: string[]) => void;
  onRowClick?: (row: T, rowIndex: number) => void;
  enableFiltering?: boolean;
  enableExport?: boolean;
  enableColumnVisibility?: boolean;
  enableDensityToggle?: boolean;
  enablePagination?: boolean;
  enableVirtualization?: boolean;
  serverMode?: boolean;
  totalCount?: number;
  onQueryChange?: (query: StandardTableQuery) => void;
  defaultPageSize?: number;
  className?: string;
};

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

function valueOf<T>(row: T, column: StandardTableColumn<T>): unknown {
  if (typeof column.accessor === "function") return column.accessor(row);
  if (typeof column.accessor === "string") return row[column.accessor];
  return "";
}

function toText(value: unknown): string {
  if (value === null || value === undefined) return "";
  return String(value);
}

function escapeCsv(value: unknown): string {
  const text = toText(value);
  if (text.includes(",") || text.includes("\n") || text.includes('"')) {
    return '"' + text.replace(/"/g, '""') + '"';
  }
  return text;
}

function downloadCsv(filename: string, headers: string[], rows: string[][]) {
  const csv = [headers.map(escapeCsv).join(","), ...rows.map((row) => row.map(escapeCsv).join(","))].join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export function StandardTable<T>({
  columns,
  data,
  getRowKey,
  caption,
  isLoading = false,
  hasError = false,
  errorMessage = "Refreshing data did not complete. Retry when the source is available.",
  onRetry,
  emptyTitle = "No records available",
  emptyDescription = "Adjust filters or refresh the data source.",
  primaryAction,
  selectionMode = "none",
  selectedRowKeys,
  onSelectionChange,
  onRowClick,
  enableFiltering = false,
  enableExport = false,
  enableColumnVisibility = false,
  enableDensityToggle = false,
  enablePagination = false,
  enableVirtualization = false,
  serverMode = false,
  totalCount,
  onQueryChange,
  defaultPageSize = 25,
  className,
}: StandardTableProps<T>) {
  const [filter, setFilter] = useState("");
  const [density, setDensity] = useState<StandardTableDensity>("comfortable");
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const [sorting, setSorting] = useState<Array<{ key: string; direction: StandardTableSortDirection }>>([]);
  const [hiddenColumns, setHiddenColumns] = useState<string[]>([]);
  const [internalSelected, setInternalSelected] = useState<string[]>([]);

  const selected = selectedRowKeys ? [...selectedRowKeys] : internalSelected;

  const visibleColumns = columns.filter((column) => !column.hidden && !hiddenColumns.includes(column.key));

  function emitQuery(next?: Partial<StandardTableQuery>) {
    onQueryChange?.({
      pageIndex,
      pageSize,
      filter,
      sorting,
      ...next,
    });
  }

  function toggleSort(column: StandardTableColumn<T>, shiftKey: boolean) {
    if (!column.sortable) return;

    setSorting((current) => {
      const existing = current.find((item) => item.key === column.key);
      const nextDirection: StandardTableSortDirection = existing?.direction === "asc" ? "desc" : "asc";
      const next = shiftKey
        ? [...current.filter((item) => item.key !== column.key), { key: column.key, direction: nextDirection }]
        : [{ key: column.key, direction: nextDirection }];

      emitQuery({ sorting: next });
      return next;
    });
  }

  const processed = useMemo(() => {
    let rows = [...data];

    if (!serverMode && filter.trim()) {
      const q = filter.trim().toLowerCase();
      rows = rows.filter((row) =>
        visibleColumns.some((column) => toText(valueOf(row, column)).toLowerCase().includes(q)),
      );
    }

    if (!serverMode && sorting.length > 0) {
      rows.sort((a, b) => {
        for (const item of sorting) {
          const column = visibleColumns.find((candidate) => candidate.key === item.key);
          if (!column) continue;

          const av = toText(valueOf(a, column));
          const bv = toText(valueOf(b, column));
          const compare = av.localeCompare(bv, undefined, { numeric: true, sensitivity: "base" });

          if (compare !== 0) {
            return item.direction === "asc" ? compare : -compare;
          }
        }
        return 0;
      });
    }

    return rows;
  }, [data, filter, serverMode, sorting, visibleColumns]);

  const pageCount = Math.max(1, Math.ceil((totalCount ?? processed.length) / pageSize));

  const paged = useMemo(() => {
    if (!enablePagination || serverMode) return processed;
    return processed.slice(pageIndex * pageSize, pageIndex * pageSize + pageSize);
  }, [enablePagination, pageIndex, pageSize, processed, serverMode]);

  const displayed = enableVirtualization && paged.length > 500 ? paged.slice(0, 500) : paged;

  function setSelection(keys: string[]) {
    if (!selectedRowKeys) setInternalSelected(keys);
    onSelectionChange?.(keys);
  }

  function toggleRow(key: string) {
    if (selectionMode === "none") return;

    if (selectionMode === "single") {
      setSelection(selected.includes(key) ? [] : [key]);
      return;
    }

    setSelection(selected.includes(key) ? selected.filter((item) => item !== key) : [...selected, key]);
  }

  if (hasError) {
    return (
      <div className={cx("ppiq-std-table-shell", className)}>
        <div className="ppiq-std-table-state">
          <strong>Refreshing table</strong>
          <span>{errorMessage}</span>
          {onRetry ? (
            <div style={{ marginTop: 14 }}>
              <StandardButton variant="secondary" leadingIcon={<RefreshCw size={16} />} onClick={onRetry}>
                Retry
              </StandardButton>
            </div>
          ) : null}
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className={cx("ppiq-std-table-shell", className)}>
        <div className="ppiq-std-table-scroll">
          <table className={cx("ppiq-std-table", "ppiq-std-table--" + density)} role="table">
            <thead>
              <tr role="row">
                {visibleColumns.map((column) => (
                  <th key={column.key} role="columnheader">
                    {column.header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {Array.from({ length: 8 }).map((_, rowIndex) => (
                <tr key={rowIndex} role="row">
                  {visibleColumns.map((column) => (
                    <td key={column.key} role="cell">
                      <div className="ppiq-std-table-skeleton" />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  if (processed.length === 0) {
    return (
      <div className={cx("ppiq-std-table-shell", className)}>
        <div className="ppiq-std-table-state">
          <strong>{emptyTitle}</strong>
          <span>{emptyDescription}</span>
          {primaryAction ? <div style={{ marginTop: 14 }}>{primaryAction}</div> : null}
        </div>
      </div>
    );
  }

  return (
    <div className={cx("ppiq-std-table-shell", className)}>
      <div className="ppiq-std-table-toolbar">
        <div className="ppiq-std-table-toolbar__left">
          {enableFiltering ? (
            <StandardInput
              type="search"
              value={filter}
              onChange={(value) => {
                setFilter(value);
                setPageIndex(0);
                emitQuery({ filter: value, pageIndex: 0 });
              }}
              leadingIcon={<Search size={16} />}
              placeholder="Filter table..."
              aria-label="Filter table"
            />
          ) : null}
        </div>

        <div className="ppiq-std-table-toolbar__right">
          {enableDensityToggle ? (
            <StandardSelect
              value={density}
              onChange={(value) => setDensity(String(value) as StandardTableDensity)}
              options={[
                { value: "compact", label: "Compact" },
                { value: "comfortable", label: "Comfortable" },
                { value: "spacious", label: "Spacious" },
              ]}
              aria-label="Table density"
            />
          ) : null}

          {enableColumnVisibility ? (
            <StandardSelect
              multiple
              value={hiddenColumns}
              placeholder="Hide columns"
              onChange={(value) => setHiddenColumns(Array.isArray(value) ? value : [])}
              options={columns.map((column) => ({ value: column.key, label: column.header }))}
            />
          ) : null}

          {enableExport ? (
            <StandardButton
              variant="secondary"
              leadingIcon={<Download size={16} />}
              onClick={() =>
                downloadCsv(
                  "plantprocess-table-export.csv",
                  visibleColumns.map((column) => String(column.header)),
                  processed.map((row, rowIndex) =>
                    visibleColumns.map((column) =>
                      toText(column.cell ? column.cell(row, rowIndex) : valueOf(row, column)),
                    ),
                  ),
                )
              }
            >
              Export CSV
            </StandardButton>
          ) : null}
        </div>
      </div>

      <div className="ppiq-std-table-scroll">
        <table className={cx("ppiq-std-table", "ppiq-std-table--" + density)} role="table">
          {caption ? <caption>{caption}</caption> : null}
          <thead>
            <tr role="row">
              {selectionMode !== "none" ? (
                <th role="columnheader" style={{ width: 44 }}>
                  Select
                </th>
              ) : null}

              {visibleColumns.map((column) => {
                const sortState = sorting.find((item) => item.key === column.key);

                return (
                  <th
                    key={column.key}
                    role="columnheader"
                    aria-sort={sortState ? (sortState.direction === "asc" ? "ascending" : "descending") : "none"}
                    style={{ width: column.width, minWidth: column.minWidth }}
                  >
                    <button
                      type="button"
                      className="ppiq-std-table__header-button"
                      disabled={!column.sortable}
                      onClick={(event) => toggleSort(column, event.shiftKey)}
                    >
                      <span>{column.header}</span>
                      {sortState?.direction === "asc" ? <ChevronUp size={14} /> : null}
                      {sortState?.direction === "desc" ? <ChevronDown size={14} /> : null}
                    </button>
                    <span className="ppiq-std-table__resize-handle" title="Column resize handle" />
                  </th>
                );
              })}
            </tr>
          </thead>

          <tbody>
            {displayed.map((row, rowIndex) => {
              const key = getRowKey(row, rowIndex);
              const isSelected = selected.includes(key);

              return (
                <tr
                  key={key}
                  role="row"
                  aria-selected={isSelected || undefined}
                  tabIndex={onRowClick ? 0 : undefined}
                  onClick={() => onRowClick?.(row, rowIndex)}
                >
                  {selectionMode !== "none" ? (
                    <td role="cell">
                      <input
                        type={selectionMode === "single" ? "radio" : "checkbox"}
                        checked={isSelected}
                        aria-label={"Select row " + key}
                        onChange={() => toggleRow(key)}
                        onClick={(event) => event.stopPropagation()}
                      />
                    </td>
                  ) : null}

                  {visibleColumns.map((column) => (
                    <td
                      key={column.key}
                      role="cell"
                      style={{ textAlign: column.align ?? "left" }}
                    >
                      {column.cell ? column.cell(row, rowIndex) : toText(valueOf(row, column))}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {enablePagination ? (
        <div className="ppiq-std-table-pagination">
          <span>
            Page {pageIndex + 1} of {pageCount} · {totalCount ?? processed.length} rows
          </span>

          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <StandardSelect
              value={String(pageSize)}
              onChange={(value) => {
                const next = Number(value);
                setPageSize(next);
                setPageIndex(0);
                emitQuery({ pageSize: next, pageIndex: 0 });
              }}
              options={[5, 10, 25, 50, 100].map((size) => ({ value: String(size), label: String(size) }))}
            />

            <StandardButton
              variant="ghost"
              isDisabled={pageIndex === 0}
              onClick={() => {
                const next = Math.max(0, pageIndex - 1);
                setPageIndex(next);
                emitQuery({ pageIndex: next });
              }}
            >
              Previous
            </StandardButton>

            <StandardInput
              type="number"
              value={String(pageIndex + 1)}
              onChange={(value) => {
                const next = Math.max(0, Math.min(pageCount - 1, Number(value || 1) - 1));
                setPageIndex(next);
                emitQuery({ pageIndex: next });
              }}
              aria-label="Jump to page"
              style={{ width: 72 }}
            />

            <StandardButton
              variant="ghost"
              isDisabled={pageIndex >= pageCount - 1}
              onClick={() => {
                const next = Math.min(pageCount - 1, pageIndex + 1);
                setPageIndex(next);
                emitQuery({ pageIndex: next });
              }}
            >
              Next
            </StandardButton>
          </div>
        </div>
      ) : null}

      {enableVirtualization && paged.length > 500 ? (
        <div className="ppiq-std-table-pagination">
          Virtualized window active: showing first 500 visible rows from {paged.length}. Full implementation can switch to @tanstack/react-virtual without changing the public props.
        </div>
      ) : null}
    </div>
  );
}
