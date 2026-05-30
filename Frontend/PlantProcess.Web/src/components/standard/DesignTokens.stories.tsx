
import type { Meta, StoryObj } from "@storybook/react-vite";
import { ppiqTokens } from "./tokens";
import { StandardCard } from "./StandardSurface";
import "./standard-components.css";

const meta: Meta = {
  title: "PlantProcess IQ/Standards/Design Tokens",
  parameters: { layout: "fullscreen" },
};

export default meta;

type Story = StoryObj;

function Token({ name, value }: { name: string; value: string }) {
  return (
    <div className="ppiq-std-token">
      <div className="ppiq-std-token__swatch" style={{ background: value }} />
      <div className="ppiq-std-token__body">
        <div className="ppiq-std-token__name">{name}</div>
        <div className="ppiq-std-token__value">{value}</div>
      </div>
    </div>
  );
}

export const Tokens: Story = {
  render: () => (
    <main className="ppiq-std-standards-page">
      <StandardCard title="Design Tokens" subtitle="All Standard* components use these tokens.">
        <h3>Colors</h3>
        <div className="ppiq-std-token-grid">
          {Object.entries(ppiqTokens.color).map(([name, value]) => (
            <Token key={name} name={name} value={value} />
          ))}
        </div>

        <h3>Radius</h3>
        <pre>{JSON.stringify(ppiqTokens.radius, null, 2)}</pre>

        <h3>Spacing</h3>
        <pre>{JSON.stringify(ppiqTokens.spacing, null, 2)}</pre>

        <h3>Elevation</h3>
        <pre>{JSON.stringify(ppiqTokens.elevation, null, 2)}</pre>
      </StandardCard>
    </main>
  ),
};
