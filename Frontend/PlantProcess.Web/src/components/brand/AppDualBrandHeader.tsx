import { SOUBrand } from "./SOUBrand";
import { ProductBrand } from "./ProductBrand";
import "./app-dual-brand-header.css";

type AppDualBrandHeaderProps = {
  licenseTier?: "Light" | "Pro" | "Pro Plus" | "Enterprise" | "Demo";
};

export function AppDualBrandHeader({
  licenseTier = "Demo",
}: AppDualBrandHeaderProps) {
  return (
    <div className="app-dual-brand-header">
      <div className="app-dual-brand-header__left">
        <SOUBrand compact />
        <span className="app-dual-brand-header__divider" aria-hidden="true" />
        <ProductBrand />
      </div>

      <div className="app-dual-brand-header__right">
        <span className="app-dual-brand-header__caption">
          Manufacturing Intelligence
        </span>
        <span className="app-dual-brand-header__license">
          {licenseTier}
        </span>
      </div>
    </div>
  );
}
