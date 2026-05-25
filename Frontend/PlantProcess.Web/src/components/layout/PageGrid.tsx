import type { ReactNode } from "react";

export type PageGridProps = {
  children: ReactNode;
  columns?: 1 | 2 | 3 | 4;
  density?: "normal" | "compact";
};

export function PageGrid({
  children,
  columns = 2,
  density = "normal",
}: PageGridProps) {
  return (
    <div
      className={`page-grid page-grid--${columns} page-grid--${density}`}
    >
      {children}
    </div>
  );
}

export function PagePanel({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return <section className={`page-panel ${className ?? ""}`}>{children}</section>;
}

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
}: {
  eyebrow?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="page-header-standard">
      <div>
        {eyebrow ? <div className="page-header-standard__eyebrow">{eyebrow}</div> : null}
        <h1>{title}</h1>
        {description ? <p>{description}</p> : null}
      </div>

      {actions ? <div className="page-header-standard__actions">{actions}</div> : null}
    </header>
  );
}