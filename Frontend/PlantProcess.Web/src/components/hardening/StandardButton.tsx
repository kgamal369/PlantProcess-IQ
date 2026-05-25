import type { ButtonHTMLAttributes, ReactNode } from "react";

type Variant = "primary" | "secondary" | "danger" | "ghost";

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  isBusy?: boolean;
  icon?: ReactNode;
};

export function StandardButton({
  variant = "secondary",
  isBusy = false,
  icon,
  disabled,
  children,
  className,
  type = "button",
  ...rest
}: Props) {
  const classes = [
    "standard-button",
    `standard-button--${variant}`,
    isBusy ? "standard-button--busy" : "",
    className ?? "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button
      {...rest}
      type={type}
      className={classes}
      disabled={disabled || isBusy}
      aria-busy={isBusy}
    >
      {icon ? <span className="standard-button__icon">{icon}</span> : null}
      <span>{isBusy ? "Working…" : children}</span>
    </button>
  );
}