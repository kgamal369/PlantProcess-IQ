import {
  forwardRef,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type SelectHTMLAttributes,
  type TableHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";

import "./standard-components.css";

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export type StandardPageButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  isDisabled?: boolean;
  isLoading?: boolean;
};

export const StandardPageButton = forwardRef<HTMLButtonElement, StandardPageButtonProps>(
  ({ className, type = "button", disabled, isDisabled, isLoading, children, ...rest }, ref) => (
    <button
      ref={ref}
      type={type}
      className={cx("ppiq-std-button", "ppiq-std-button--md", className)}
      disabled={disabled || isDisabled || isLoading}
      aria-disabled={disabled || isDisabled || isLoading || undefined}
      aria-busy={isLoading || undefined}
      {...rest}
    >
      {children}
    </button>
  ),
);

StandardPageButton.displayName = "StandardPageButton";

export const StandardPageInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...rest }, ref) => (
    <input
      ref={ref}
      className={cx("ppiq-std-field__control", className)}
      {...rest}
    />
  ),
);

StandardPageInput.displayName = "StandardPageInput";

export const StandardPageSelect = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  ({ className, children, ...rest }, ref) => (
    <select
      ref={ref}
      className={cx("ppiq-std-field__control", className)}
      {...rest}
    >
      {children}
    </select>
  ),
);

StandardPageSelect.displayName = "StandardPageSelect";

export const StandardPageTextArea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  ({ className, ...rest }, ref) => (
    <textarea
      ref={ref}
      className={cx("ppiq-std-field__control", "ppiq-std-field__textarea", className)}
      {...rest}
    />
  ),
);

StandardPageTextArea.displayName = "StandardPageTextArea";

export const StandardPageTable = forwardRef<HTMLTableElement, TableHTMLAttributes<HTMLTableElement>>(
  ({ className, children, ...rest }, ref) => (
    <table
      ref={ref}
      className={cx("ppiq-std-table", className)}
      {...rest}
    >
      {children}
    </table>
  ),
);

StandardPageTable.displayName = "StandardPageTable";
