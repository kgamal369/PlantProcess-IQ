import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from "recharts";
import type { DashboardFilters } from "../../api/plantProcessApi";
import {
  useDashboardSelections,
  type DashboardSelectionType,
} from "../../state/DashboardSelectionContext";
import { EmptyInsightState } from "../dashboard/EmptyInsightState";

export interface ChartRow {
  [key: string]: string | number | null | undefined;
}

interface SelectionConfig {
  type: DashboardSelectionType;
  field: keyof DashboardFilters;
  sourceWidget: string;
  valueKey?: string;
  labelKey?: string;
}

interface InteractiveChartProps {
  data: ChartRow[];
  categoryKey: string;
  valueKey: string;
  selection: SelectionConfig;
  height?: number;
}

const palette = [
  "#2563eb",
  "#16a34a",
  "#dc2626",
  "#d97706",
  "#7c3aed",
  "#0891b2",
  "#be123c",
  "#4f46e5",
  "#0f766e",
  "#9333ea",
];

export function InteractiveBarChart({
  data,
  categoryKey,
  valueKey,
  selection,
  height = 300,
}: InteractiveChartProps) {
  const { applySelection, openDrilldown } = useDashboardSelections();

  if (!data.length) return <EmptyInsightState />;

  function selectRow(row: ChartRow) {
    const value = row[selection.valueKey ?? categoryKey];
    const label = row[selection.labelKey ?? categoryKey];

    if (value === null || value === undefined) return;

    applySelection({
      type: selection.type,
      field: selection.field,
      value,
      label: String(label ?? value),
      sourceWidget: selection.sourceWidget,
    });

    openDrilldown({
      title: String(label ?? value),
      subtitle: selection.sourceWidget,
      type: selection.type,
      payload: row,
    });
  }

  return (
    <div className="chart-box">
      <ResponsiveContainer width="100%" height={height}>
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey={categoryKey} />
          <YAxis />
          <Tooltip />
          <Bar dataKey={valueKey} onClick={(row: any) => selectRow(row)}>
            {data.map((_, index) => (
              <Cell
                key={index}
                fill={palette[index % palette.length]}
                className="interactive-chart-item"
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}

export function InteractivePieChart({
  data,
  categoryKey,
  valueKey,
  selection,
  height = 300,
  donut = false,
}: InteractiveChartProps & { donut?: boolean }) {
  const { applySelection, openDrilldown } = useDashboardSelections();

  if (!data.length) return <EmptyInsightState />;

  function selectRow(row: ChartRow) {
    const value = row[selection.valueKey ?? categoryKey];
    const label = row[selection.labelKey ?? categoryKey];

    if (value === null || value === undefined) return;

    applySelection({
      type: selection.type,
      field: selection.field,
      value,
      label: String(label ?? value),
      sourceWidget: selection.sourceWidget,
    });

    openDrilldown({
      title: String(label ?? value),
      subtitle: selection.sourceWidget,
      type: selection.type,
      payload: row,
    });
  }

  return (
    <div className="chart-box">
      <ResponsiveContainer width="100%" height={height}>
        <PieChart>
          <Tooltip />
          <Legend />
          <Pie
            data={data}
            dataKey={valueKey}
            nameKey={categoryKey}
            innerRadius={donut ? 70 : 0}
            outerRadius={105}
            paddingAngle={donut ? 2 : 0}
            onClick={(row: any) => selectRow(row)}
          >
            {data.map((_, index) => (
              <Cell
                key={index}
                fill={palette[index % palette.length]}
                className="interactive-chart-item"
              />
            ))}
          </Pie>
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
}

export function InteractiveLineChart({
  data,
  categoryKey,
  valueKey,
  selection,
  height = 300,
  area = false,
}: InteractiveChartProps & { area?: boolean }) {
  const { applySelection, openDrilldown } = useDashboardSelections();

  if (!data.length) return <EmptyInsightState />;

  function selectRow(row: ChartRow) {
    const value = row[selection.valueKey ?? categoryKey];
    const label = row[selection.labelKey ?? categoryKey];

    if (value === null || value === undefined) return;

    applySelection({
      type: selection.type,
      field: selection.field,
      value,
      label: String(label ?? value),
      sourceWidget: selection.sourceWidget,
    });

    openDrilldown({
      title: String(label ?? value),
      subtitle: selection.sourceWidget,
      type: selection.type,
      payload: row,
    });
  }

  const commonProps = {
    data,
    onClick: (state: any) => {
      const row = state?.activePayload?.[0]?.payload;
      if (row) selectRow(row);
    },
  };

  if (area) {
    return (
      <div className="chart-box">
        <ResponsiveContainer width="100%" height={height}>
          <AreaChart {...commonProps}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey={categoryKey} />
            <YAxis />
            <Tooltip />
            <Area
              type="monotone"
              dataKey={valueKey}
              stroke="#2563eb"
              fill="#bfdbfe"
              strokeWidth={3}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    );
  }

  return (
    <div className="chart-box">
      <ResponsiveContainer width="100%" height={height}>
        <LineChart {...commonProps}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey={categoryKey} />
          <YAxis />
          <Tooltip />
          <Line
            type="monotone"
            dataKey={valueKey}
            stroke="#2563eb"
            strokeWidth={3}
            dot={{ r: 4 }}
            activeDot={{ r: 7 }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}

export function InteractiveScatterChart({
  data,
  xKey,
  yKey,
  zKey,
  labelKey,
  selection,
  height = 320,
}: {
  data: ChartRow[];
  xKey: string;
  yKey: string;
  zKey?: string;
  labelKey: string;
  selection: SelectionConfig;
  height?: number;
}) {
  const { applySelection, openDrilldown } = useDashboardSelections();

  if (!data.length) return <EmptyInsightState />;

  function selectRow(row: ChartRow) {
    const value = row[selection.valueKey ?? labelKey];
    const label = row[selection.labelKey ?? labelKey];

    if (value === null || value === undefined) return;

    applySelection({
      type: selection.type,
      field: selection.field,
      value,
      label: String(label ?? value),
      sourceWidget: selection.sourceWidget,
    });

    openDrilldown({
      title: String(label ?? value),
      subtitle: selection.sourceWidget,
      type: selection.type,
      payload: row,
    });
  }

  return (
    <div className="chart-box">
      <ResponsiveContainer width="100%" height={height}>
        <ScatterChart>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey={xKey} name={xKey} />
          <YAxis dataKey={yKey} name={yKey} />
          {zKey ? <ZAxis dataKey={zKey} range={[60, 260]} /> : null}
          <Tooltip cursor={{ strokeDasharray: "3 3" }} />
          <Scatter
            data={data}
            fill="#2563eb"
            onClick={(row: any) => selectRow(row)}
          />
        </ScatterChart>
      </ResponsiveContainer>
    </div>
  );
}

export function InteractiveHeatmap({
  data,
  xKey,
  yKey,
  valueKey,
  selection,
}: {
  data: ChartRow[];
  xKey: string;
  yKey: string;
  valueKey: string;
  selection: SelectionConfig;
}) {
  const { applySelection, openDrilldown } = useDashboardSelections();

  if (!data.length) return <EmptyInsightState />;

  const xValues = Array.from(new Set(data.map((row) => String(row[xKey] ?? "-"))));
  const yValues = Array.from(new Set(data.map((row) => String(row[yKey] ?? "-"))));

  const maxValue = Math.max(
    ...data.map((row) => Number(row[valueKey] ?? 0)),
    1
  );

  function findCell(x: string, y: string) {
    return data.find(
      (row) => String(row[xKey] ?? "-") === x && String(row[yKey] ?? "-") === y
    );
  }

  function selectCell(row: ChartRow | undefined) {
    if (!row) return;

    const value = row[selection.valueKey ?? yKey];
    const label = row[selection.labelKey ?? yKey];

    if (value === null || value === undefined) return;

    applySelection({
      type: selection.type,
      field: selection.field,
      value,
      label: String(label ?? value),
      sourceWidget: selection.sourceWidget,
    });

    openDrilldown({
      title: String(label ?? value),
      subtitle: selection.sourceWidget,
      type: selection.type,
      payload: row,
    });
  }

  return (
    <div className="heatmap">
      <div className="heatmap__header" />
      {xValues.map((x) => (
        <div key={x} className="heatmap__axis heatmap__axis--x">
          {x}
        </div>
      ))}

      {yValues.map((y) => (
        <div key={y} className="heatmap__row">
          <div className="heatmap__axis heatmap__axis--y">{y}</div>

          {xValues.map((x) => {
            const row = findCell(x, y);
            const value = Number(row?.[valueKey] ?? 0);
            const intensity = Math.max(0.08, value / maxValue);

            return (
              <button
                key={`${x}-${y}`}
                className="heatmap__cell"
                style={{
                  opacity: intensity,
                }}
                onClick={() => selectCell(row)}
                type="button"
                title={`${x} / ${y}: ${value}`}
              >
                {value}
              </button>
            );
          })}
        </div>
      ))}
    </div>
  );
}