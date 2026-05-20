import "./product-brand.css";

type ProductBrandProps = {
  compact?: boolean;
  showSubtitle?: boolean;
  className?: string;
};

export function ProductBrand({
  compact = false,
  showSubtitle = true,
  className = "",
}: ProductBrandProps) {
  return (
    <a
      className={`product-brand ${compact ? "product-brand--compact" : ""} ${className}`}
      href="/dashboard"
      aria-label="PlantProcess IQ dashboard"
    >
      <span className="product-brand__mark" aria-hidden="true">
        <span className="product-brand__node product-brand__node--a" />
        <span className="product-brand__node product-brand__node--b" />
        <span className="product-brand__node product-brand__node--c" />
        <span className="product-brand__node product-brand__node--d" />
        <span className="product-brand__line product-brand__line--ab" />
        <span className="product-brand__line product-brand__line--ac" />
        <span className="product-brand__line product-brand__line--bd" />
        <span className="product-brand__line product-brand__line--cd" />
      </span>

      {!compact && (
        <span className="product-brand__text">
          <span className="product-brand__name">
            <span>PlantProcess</span>
            <strong>IQ</strong>
          </span>

          {showSubtitle && (
            <span className="product-brand__subtitle">
              Process-to-quality intelligence
            </span>
          )}
        </span>
      )}
    </a>
  );
}
