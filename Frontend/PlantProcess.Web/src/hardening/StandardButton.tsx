import type { ButtonHTMLAttributes, ReactNode } from "react";

export type StandardButtonVariant =
  | "primary"
  | "secondary"
  | "danger"
  | "ghost"
  | "success";

export type StandardButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: StandardButtonVariant;
  isBusy?: boolean;
  icon?: ReactNode;
  busyLabel?: string;
};

export function StandardButton({
  variant = "secondary",
  isBusy = false,
  icon,
  busyLabel = "Working...",
  disabled,
  children,
  className,
  type = "button",
  ...rest
}: StandardButtonProps) {
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
      <span>{isBusy ? busyLabel : children}</span>
    </button>
  );
}

export default StandardButton;