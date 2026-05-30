
import { forwardRef, type AnchorHTMLAttributes, type ButtonHTMLAttributes, type MouseEvent, type ReactNode, type Ref } from "react";
import { Loader2 } from "lucide-react";
import "./standard-components.css";

type StandardButtonVariant = "primary" | "secondary" | "ghost" | "danger" | "success";
type StandardButtonSize = "sm" | "md" | "lg";
type StandardButtonAs = "button" | "a";

type SharedProps = {
  as?: StandardButtonAs;
  variant?: StandardButtonVariant;
  size?: StandardButtonSize;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  isLoading?: boolean;
  isDisabled?: boolean;
  fullWidth?: boolean;
  iconOnly?: boolean;
  ariaLabel?: string;
  children?: ReactNode;
};

type ButtonProps = SharedProps &
  Omit<ButtonHTMLAttributes<HTMLButtonElement>, "disabled" | "aria-label"> & {
    as?: "button";
    href?: never;
  };

type AnchorProps = SharedProps &
  Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "aria-label"> & {
    as: "a";
    href: string;
  };

export type StandardButtonProps = ButtonProps | AnchorProps;

function classes(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export const StandardButton = forwardRef<HTMLButtonElement | HTMLAnchorElement, StandardButtonProps>(
  (props, ref) => {
    const {
      as = "button",
      variant = "primary",
      size = "md",
      leadingIcon,
      trailingIcon,
      isLoading = false,
      isDisabled = false,
      fullWidth = false,
      iconOnly = false,
      ariaLabel,
      children,
      className,
      onClick,
      ...rest
    } = props;

    const disabled = isDisabled || isLoading;

    const classNames = classes(
      "ppiq-std-button",
      "ppiq-std-button--" + variant,
      "ppiq-std-button--" + size,
      fullWidth && "ppiq-std-button--full",
      iconOnly && "ppiq-std-button--icon-only",
      className,
    );

    const content = (
      <>
        {isLoading ? <Loader2 className="ppiq-std-button__spinner" size={16} aria-hidden="true" /> : leadingIcon}
        {!iconOnly ? (
          <span className={isLoading ? "ppiq-std-button__label--loading" : undefined}>
            {isLoading ? "Loading" : children}
          </span>
        ) : (
          <span className="sr-only">{ariaLabel ?? String(children ?? "Button")}</span>
        )}
        {!isLoading ? trailingIcon : null}
      </>
    );

    if (as === "a") {
      const anchorProps = rest as Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "aria-label">;
      return (
        <a
          ref={ref as Ref<HTMLAnchorElement>}
          className={classNames}
          aria-label={ariaLabel}
          aria-disabled={disabled || undefined}
          aria-busy={isLoading || undefined}
          tabIndex={disabled ? -1 : anchorProps.tabIndex}
          onClick={(event) => {
            if (disabled) {
              event.preventDefault();
              return;
            }

            const anchorOnClick = onClick as AnchorHTMLAttributes<HTMLAnchorElement>["onClick"] | undefined;
            anchorOnClick?.(event);
          }}
          {...anchorProps}
        >
          {content}
        </a>
      );
    }

    const buttonProps = rest as Omit<ButtonHTMLAttributes<HTMLButtonElement>, "disabled" | "aria-label">;

    return (
      <button
        ref={ref as Ref<HTMLButtonElement>}
        type={buttonProps.type ?? "button"}
        className={classNames}
        disabled={disabled}
        aria-label={ariaLabel}
        aria-busy={isLoading || undefined}
        aria-disabled={disabled || undefined}
        onClick={onClick as ButtonHTMLAttributes<HTMLButtonElement>["onClick"]}
        {...buttonProps}
      >
        {content}
      </button>
    );
  },
);

StandardButton.displayName = "StandardButton";
