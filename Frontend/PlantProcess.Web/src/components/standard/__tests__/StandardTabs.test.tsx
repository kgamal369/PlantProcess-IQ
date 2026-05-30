
import { describe, expect, it } from "vitest";
import { renderToString } from "react-dom/server";
import { StandardTabs } from "../StandardTabs";

describe("StandardTabs", () => {
  it("renders horizontal tabs with WAI-ARIA roles", () => {
    const html = renderToString(
      <StandardTabs
        ariaLabel="Test tabs"
        value="a"
        onChange={() => undefined}
        items={[
          { id: "a", label: "A", content: "Panel A" },
          { id: "b", label: "B", content: "Panel B", badge: "2" },
        ]}
      />,
    );

    expect(html).toContain("role=\"tablist\"");
    expect(html).toContain("role=\"tab\"");
    expect(html).toContain("role=\"tabpanel\"");
  });

  it("renders vertical tabs", () => {
    const html = renderToString(
      <StandardTabs
        ariaLabel="Vertical tabs"
        orientation="vertical"
        value="a"
        onChange={() => undefined}
        items={[
          { id: "a", label: "A", content: "Panel A" },
          { id: "b", label: "B", content: "Panel B" },
        ]}
      />,
    );

    expect(html).toContain("ppiq-std-tabs--vertical");
  });
});
