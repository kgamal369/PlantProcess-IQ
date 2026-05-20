import "./sou-brand.css";

type SOUBrandProps = {
  compact?: boolean;
  showTagline?: boolean;
  className?: string;
};

export function SOUBrand({
  compact = false,
  showTagline = true,
  className = "",
}: SOUBrandProps) {
  return (
    <a className={`sou-brand ${compact ? "sou-brand--compact" : ""} ${className}`} href="/" aria-label="SOU PlantProcess IQ home">
      <span className="sou-brand__iconWrap">
        <img src="/brand/sou-icon.svg" alt="" className="sou-brand__icon" />
      </span>

      {!compact && (
        <span className="sou-brand__text">
          <span className="sou-brand__company">SOU</span>
          {showTagline && (
            <span className="sou-brand__tagline">Manufacturing Intelligence</span>
          )}
        </span>
      )}
    </a>
  );
}

export function SOUMark({ className = "" }: { className?: string }) {
  return (
    <img
      src="/brand/sou-icon.svg"
      alt="SOU"
      className={`sou-mark ${className}`}
    />
  );
}
