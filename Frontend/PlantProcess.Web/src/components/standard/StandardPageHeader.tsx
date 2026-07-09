// ============================================================
// FILE: src/components/standard/StandardPageHeader.tsx
// One page header for every full-page surface: title, subtitle, description,
// live status line and optional actions.
//
// Why this exists rather than reusing StandardCard: a page header is not a
// card. It carries the h1 (exactly one per page), an aria-live status region,
// and it must not draw a card border around the top of every screen.
// ============================================================
import type { ReactNode } from "react";
import "./standard-components.css";

export type StandardPageHeaderTone = "neutral" | "good" | "warn" | "bad";

export type StandardPageHeaderProps = {
  title: ReactNode;
  subtitle?: ReactNode;
  description?: ReactNode;
  status?: ReactNode;
  statusTone?: StandardPageHeaderTone;
  actions?: ReactNode;
  children?: ReactNode;
};

export function StandardPageHeader({
  title,
  subtitle,
  description,
  status,
  statusTone = "neutral",
  actions,
  children,
}: StandardPageHeaderProps) {
  return (
    <header className="ppiq-std-page-header">
      <div className="ppiq-std-page-header__row">
        <div className="ppiq-std-page-header__titles">
          <h1 className="ppiq-std-page-header__title">{title}</h1>
          {subtitle ? <p className="ppiq-std-page-header__subtitle">{subtitle}</p> : null}
        </div>
        {actions ? <div className="ppiq-std-page-header__actions">{actions}</div> : null}
      </div>

      {description ? <p className="ppiq-std-page-header__description">{description}</p> : null}

      {status ? (
        <p
          className={"ppiq-std-page-header__status ppiq-std-page-header__status--" + statusTone}
          role="status"
          aria-live="polite"
        >
          {status}
        </p>
      ) : null}

      {children}
    </header>
  );
}