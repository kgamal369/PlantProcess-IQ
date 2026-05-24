import { useCallback, useMemo, useRef, useState, type ReactNode } from "react";
import type { SortDirection } from "@/api/plantProcessApi";

export interface SortableColumn<T> {
  key: string;
  title: string;
  sortable?: boolean;
  align?: "left" | "right" | "center";
  width?: number | string;
  render: (row: T, index: number) => ReactNode;
}

type SortableDataTableProps<T> = {
  rows: T[];
  columns: SortableColumn<T>[];
  sortBy?: string;
  sortDirection?: SortDirection;
  onSort?: (sortBy: string, sortDirection: SortDirection) => void;
  emptyText?: string;
  virtualizationThreshold?: number;
  rowHeight?: number;
  maxBodyHeight?: number;
  getRowKey?: (row: T, index: number) => string | number;
};

export function SortableDataTable<T>({
  rows,
  columns,
  sortBy,
  sortDirection,
  onSort,
  emptyText = "No data available.",
  virtualizationThreshold = 200,
  rowHeight = 44,
  maxBodyHeight = 560,
  getRowKey,
}: SortableDataTableProps<T>) {
  const shouldVirtualize = rows.length > virtualizationThreshold;

  const [scrollTop, setScrollTop] = useState(0);
  const bodyRef = useRef<HTMLDivElement | null>(null);

  const handleSort = useCallback(
    (column: SortableColumn<T>) => {
      if (!column.sortable || !onSort) return;

      const nextDirection: SortDirection =
        sortBy === column.key && sortDirection === "asc" ? "desc" : "asc";

      onSort(column.key, nextDirection);
    },
    [onSort, sortBy, sortDirection]
  );

  const virtualState = useMemo(() => {
    if (!shouldVirtualize) {
      return {
        startIndex: 0,
        endIndex: rows.length,
        beforeHeight: 0,
        afterHeight: 0,
        visibleRows: rows,
      };
    }

    const overscan = 12;
    const viewportHeight = maxBodyHeight;
    const startIndex = Math.max(0, Math.floor(scrollTop / rowHeight) - overscan);
    const visibleCount = Math.ceil(viewportHeight / rowHeight) + overscan * 2;
    const endIndex = Math.min(rows.length, startIndex + visibleCount);
    const beforeHeight = startIndex * rowHeight;
    const afterHeight = Math.max(0, (rows.length - endIndex) * rowHeight);

    return {
      startIndex,
      endIndex,
      beforeHeight,
      afterHeight,
      visibleRows: rows.slice(startIndex, endIndex),
    };
  }, [maxBodyHeight, rowHeight, rows, scrollTop, shouldVirtualize]);

  const handleScroll = useCallback(() => {
    if (!bodyRef.current) return;
    setScrollTop(bodyRef.current.scrollTop);
  }, []);

  return (
    <div className="table-wrap sortable-data-table">
      <div className="sortable-data-table__summary">
        <span>{rows.length.toLocaleString()} rows</span>
        {shouldVirtualize ? <span>Virtualized rendering enabled</span> : null}
      </div>

      <div
        ref={bodyRef}
        className={shouldVirtualize ? "virtual-table-scroll" : undefined}
        style={shouldVirtualize ? { maxHeight: maxBodyHeight, overflow: "auto" } : undefined}
        onScroll={shouldVirtualize ? handleScroll : undefined}
      >
        <table>
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  style={{
                    textAlign: column.align ?? "left",
                    width: column.width,
                  }}
                  className={column.sortable ? "sortable-header" : undefined}
                  onClick={() => handleSort(column)}
                  aria-sort={
                    column.sortable && sortBy === column.key
                      ? sortDirection === "asc"
                        ? "ascending"
                        : "descending"
                      : undefined
                  }
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
              <>
                {shouldVirtualize && virtualState.beforeHeight > 0 ? (
                  <tr aria-hidden="true">
                    <td
                      colSpan={columns.length}
                      style={{
                        height: virtualState.beforeHeight,
                        padding: 0,
                        border: 0,
                      }}
                    />
                  </tr>
                ) : null}

                {virtualState.visibleRows.map((row, localIndex) => {
                  const index = shouldVirtualize
                    ? virtualState.startIndex + localIndex
                    : localIndex;

                  const key = getRowKey ? getRowKey(row, index) : index;

                  return (
                    <tr key={key} style={shouldVirtualize ? { height: rowHeight } : undefined}>
                      {columns.map((column) => (
                        <td
                          key={column.key}
                          style={{
                            textAlign: column.align ?? "left",
                            width: column.width,
                          }}
                        >
                          {column.render(row, index)}
                        </td>
                      ))}
                    </tr>
                  );
                })}

                {shouldVirtualize && virtualState.afterHeight > 0 ? (
                  <tr aria-hidden="true">
                    <td
                      colSpan={columns.length}
                      style={{
                        height: virtualState.afterHeight,
                        padding: 0,
                        border: 0,
                      }}
                    />
                  </tr>
                ) : null}
              </>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}