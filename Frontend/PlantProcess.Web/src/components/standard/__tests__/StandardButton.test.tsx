
import { describe, expect, it } from "vitest";
import { renderToString } from "react-dom/server";
import { StandardButton } from "../StandardButton";

const variants = ["primary", "secondary", "ghost", "danger", "success"] as const;
const sizes = ["sm", "md", "lg"] as const;

describe("StandardButton", () => {
  for (const variant of variants) {
    for (const size of sizes) {
      it("renders snapshot for " + variant + " " + size, () => {
        expect(
          renderToString(
            <StandardButton variant={variant} size={size}>
              Button
            </StandardButton>,
          ),
        ).toMatchSnapshot();
      });

      it("renders disabled snapshot for " + variant + " " + size, () => {
        expect(
          renderToString(
            <StandardButton variant={variant} size={size} isDisabled>
              Disabled
            </StandardButton>,
          ),
        ).toMatchSnapshot();
      });

      it("renders loading snapshot for " + variant + " " + size, () => {
        expect(
          renderToString(
            <StandardButton variant={variant} size={size} isLoading>
              Loading
            </StandardButton>,
          ),
        ).toMatchSnapshot();
      });
    }
  }

  it("renders anchor mode", () => {
    const html = renderToString(
      <StandardButton as="a" href="/demo" variant="secondary">
        Go to demo
      </StandardButton>,
    );

    expect(html).toContain("href");
    expect(html).toContain("/demo");
  });

  it("renders icon-only accessible label", () => {
    const html = renderToString(
      <StandardButton iconOnly ariaLabel="Refresh">
        R
      </StandardButton>,
    );

    expect(html).toContain("Refresh");
  });
});
