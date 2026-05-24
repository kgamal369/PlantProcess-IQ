import { memo } from "react";

type DragGhostProps = {
  title: string;
  subtitle?: string;
  width?: number;
  height?: number;
};

export const DragGhost = memo(function DragGhost({
  title,
  subtitle,
  width = 260,
  height = 140,
}: DragGhostProps) {
  return (
    <div
      className="drag-ghost-card"
      style={{ width, height }}
      aria-label={`Dragging ${title}`}
    >
      <div className="drag-ghost-card__bar" />
      <strong>{title}</strong>
      {subtitle ? <span>{subtitle}</span> : null}
      <div className="drag-ghost-card__grid">
        <i />
        <i />
        <i />
        <i />
      </div>
    </div>
  );
});