
import type { Preview } from "@storybook/react-vite";
import "../src/index.css";
import "../src/components/standard/standard-components.css";
import "./preview.css";

const preview: Preview = {
  parameters: {
    layout: "fullscreen",
    backgrounds: {
      default: "PlantProcess IQ Dark",
      values: [
        { name: "PlantProcess IQ Dark", value: "#050B18" },
        { name: "Light verification", value: "#F7FAFC" },
      ],
    },
    docs: {
      toc: true,
    },
  },
};

export default preview;
