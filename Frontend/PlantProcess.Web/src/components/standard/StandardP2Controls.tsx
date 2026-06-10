
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  SelectHTMLAttributes,
  TableHTMLAttributes,
  TextareaHTMLAttributes,
} from "react";
import "./standard-components.css";
import "./standard-p2-controls.css";

export const P2T08_STANDARD_ROLLOUT_MARKER =
  "PPIQ_P2_T08_STANDARD_COMPONENT_ROLLOUT_BLOCKING";

type StandardP2ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "danger" | "ghost" | "action";
};

export function StandardP2Button({
  className,
  variant = "secondary",
  type = "button",
  ...props
}: StandardP2ButtonProps) {
  return (
    <button
      {...props}
      type={type}
      className={["standard-p2-control standard-p2-button", "standard-p2-button--" + variant, className]
        .filter(Boolean)
        .join(" ")}
    />
  );
}

export function StandardP2Input({
  className,
  ...props
}: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={["standard-p2-control standard-p2-field", className].filter(Boolean).join(" ")}
    />
  );
}

export function StandardP2Select({
  className,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className={["standard-p2-control standard-p2-field standard-p2-select", className].filter(Boolean).join(" ")}
    />
  );
}

export function StandardP2TextArea({
  className,
  ...props
}: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={["standard-p2-control standard-p2-field standard-p2-textarea", className].filter(Boolean).join(" ")}
    />
  );
}

export function StandardP2Table({
  className,
  ...props
}: TableHTMLAttributes<HTMLTableElement>) {
  return (
    <table
      {...props}
      className={["standard-p2-table", className].filter(Boolean).join(" ")}
    />
  );
}
