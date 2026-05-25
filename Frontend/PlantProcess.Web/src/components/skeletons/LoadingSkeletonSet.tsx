export function SkeletonLine({
  width = "100%",
}: {
  width?: string;
}) {
  return <span className="ppiq-skeleton-line" style={{ width }} />;
}

export function SkeletonCard() {
  return (
    <div className="ppiq-skeleton-card">
      <SkeletonLine width="42%" />
      <SkeletonLine width="88%" />
      <SkeletonLine width="76%" />
      <SkeletonLine width="64%" />
    </div>
  );
}

export function SkeletonKpiStrip({ count = 4 }: { count?: number }) {
  return (
    <div className="ppiq-skeleton-kpi-strip">
      {Array.from({ length: count }).map((_, index) => (
        <div className="ppiq-skeleton-kpi" key={index}>
          <SkeletonLine width="36%" />
          <SkeletonLine width="64%" />
        </div>
      ))}
    </div>
  );
}

export function SkeletonTable({ rows = 6 }: { rows?: number }) {
  return (
    <div className="ppiq-skeleton-table">
      <div className="ppiq-skeleton-table__header">
        <SkeletonLine width="18%" />
        <SkeletonLine width="22%" />
        <SkeletonLine width="16%" />
        <SkeletonLine width="20%" />
      </div>

      {Array.from({ length: rows }).map((_, index) => (
        <div className="ppiq-skeleton-table__row" key={index}>
          <SkeletonLine width="22%" />
          <SkeletonLine width="34%" />
          <SkeletonLine width="18%" />
          <SkeletonLine width="12%" />
        </div>
      ))}
    </div>
  );
}

export function SkeletonChart() {
  return (
    <div className="ppiq-skeleton-chart">
      <div className="ppiq-skeleton-chart__bars">
        {Array.from({ length: 9 }).map((_, index) => (
          <span
            key={index}
            style={{
              height: `${32 + ((index * 17) % 58)}%`,
            }}
          />
        ))}
      </div>
    </div>
  );
}

export function LoadingSkeletonSet() {
  return (
    <div className="ppiq-loading-skeleton-set">
      <SkeletonKpiStrip />
      <div className="page-grid page-grid--2">
        <SkeletonChart />
        <SkeletonCard />
      </div>
      <SkeletonTable />
    </div>
  );
}