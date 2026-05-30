
import type { Meta, StoryObj } from "@storybook/react-vite";
import { StandardTable, type StandardTableColumn } from "./StandardTable";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

type Row = {
  id: string;
  asset: string;
  area: string;
  risk: number;
  status: string;
};

const rows: Row[] = Array.from({ length: 1000 }).map((_, index) => ({
  id: "SIG-" + String(index + 1).padStart(4, "0"),
  asset: "Production line " + String.fromCharCode(65 + (index % 4)),
  area: ["Thermal", "Mechanical", "Inspection", "Packaging"][index % 4],
  risk: Math.round((index * 17) % 100),
  status: index % 7 === 0 ? "Critical" : index % 3 === 0 ? "Watch" : "Stable",
}));

const columns: StandardTableColumn<Row>[] = [
  { key: "asset", header: "Asset", accessor: "asset", sortable: true, filterable: true },
  { key: "area", header: "Process area", accessor: "area", sortable: true },
  { key: "risk", header: "Risk", accessor: "risk", sortable: true, align: "right" },
  { key: "status", header: "Status", accessor: "status", sortable: true },
];

const meta: Meta<typeof StandardTable<Row>> = {
  title: "PlantProcess IQ/Standard/Table",
  component: StandardTable<Row>,
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj<typeof StandardTable<Row>>;

export const Populated: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable columns={columns} data={rows.slice(0, 20)} getRowKey={(row) => row.id} />
    </div>
  ),
};

export const FullFeature: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable
        columns={columns}
        data={rows}
        getRowKey={(row) => row.id}
        enableFiltering
        enableExport
        enableColumnVisibility
        enableDensityToggle
        enablePagination
        enableVirtualization
        selectionMode="multi"
      />
    </div>
  ),
};

export const Empty: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable
        columns={columns}
        data={[]}
        getRowKey={(row) => row.id}
        primaryAction={<StandardButton>Connect source</StandardButton>}
      />
    </div>
  ),
};

export const Loading: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} isLoading />
    </div>
  ),
};

export const Error: Story = {
  render: () => (
    <div className="ppiq-std-standards-page">
      <StandardTable columns={columns} data={[]} getRowKey={(row) => row.id} hasError onRetry={() => undefined} />
    </div>
  ),
};
