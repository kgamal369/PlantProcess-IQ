
import type { Meta, StoryObj } from "@storybook/react-vite";
import { Download, RefreshCw, Trash2 } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

const meta: Meta<typeof StandardButton> = {
  title: "PlantProcess IQ/Standard/Button",
  component: StandardButton,
  parameters: { layout: "centered" },
};

export default meta;

type Story = StoryObj<typeof StandardButton>;

export const Variants: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
      <StandardButton variant="primary">Primary</StandardButton>
      <StandardButton variant="secondary">Secondary</StandardButton>
      <StandardButton variant="ghost">Ghost</StandardButton>
      <StandardButton variant="danger">Danger</StandardButton>
      <StandardButton variant="success">Success</StandardButton>
    </div>
  ),
};

export const Sizes: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
      <StandardButton size="sm">Small</StandardButton>
      <StandardButton size="md">Medium</StandardButton>
      <StandardButton size="lg">Large</StandardButton>
    </div>
  ),
};

export const States: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
      <StandardButton>Default</StandardButton>
      <StandardButton isLoading>Loading</StandardButton>
      <StandardButton isDisabled>Disabled</StandardButton>
      <StandardButton leadingIcon={<RefreshCw size={16} />}>With icon</StandardButton>
      <StandardButton trailingIcon={<Download size={16} />}>Export</StandardButton>
    </div>
  ),
};

export const IconOnly: Story = {
  render: () => (
    <div style={{ display: "flex", gap: 12 }}>
      <StandardButton iconOnly ariaLabel="Refresh" leadingIcon={<RefreshCw size={16} />} />
      <StandardButton iconOnly ariaLabel="Delete" variant="danger" leadingIcon={<Trash2 size={16} />} />
    </div>
  ),
};

export const AnchorMode: Story = {
  render: () => (
    <StandardButton as="a" href="https://example.com" variant="secondary">
      Real anchor navigation
    </StandardButton>
  ),
};
