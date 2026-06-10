import {
  forwardRef,
  type AnchorHTMLAttributes,
  type ButtonHTMLAttributes,
  type ReactNode,
  type Ref,
} from "react";
import "./standard-components.css";

export const P2T09_ACTION_BUTTON_STANDARDIZATION_MARKER =
  "PPIQ_P2_T09_ACTION_BUTTON_STANDARDIZATION";

export type StandardButtonVariant =
  | "primary"
  | "secondary"
  | "ghost"
  | "danger"
  | "success"
  | "action";

export type StandardButtonSize = "sm" | "md" | "lg";

type SharedProps = {
  as?: "button" | "a";
  variant?: StandardButtonVariant;
  size?: StandardButtonSize;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  isLoading?: boolean;
  isDisabled?: boolean;
  loadingLabel?: string;
  fullWidth?: boolean;
  iconOnly?: boolean;
  ariaLabel?: string;
  children?: ReactNode;
};

type StandardButtonAsButton = SharedProps &
  Omit<ButtonHTMLAttributes<HTMLButtonElement>, "aria-label"> & {
    as?: "button";
    href?: never;
  };

type StandardButtonAsAnchor = SharedProps &
  Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "aria-label"> & {
    as: "a";
    href: string;
  };

export type StandardButtonProps = StandardButtonAsButton | StandardButtonAsAnchor;

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

function normalizeVariant(variant: StandardButtonVariant) {
  return variant === "action" ? "primary" : variant;
}

export const StandardButton = forwardRef<
  HTMLButtonElement | HTMLAnchorElement,
  StandardButtonProps
>((props, ref) => {
  const {
    as = "button",
    variant = "primary",
    size = "md",
    leadingIcon,
    trailingIcon,
    isLoading = false,
    isDisabled = false,
    loadingLabel = "Loading",
    fullWidth = false,
    iconOnly = false,
    ariaLabel,
    children,
    className,
    onClick,
    disabled,
    ...rest
  } = props as StandardButtonProps & { disabled?: boolean };

  const disabledState = Boolean(isDisabled || disabled || isLoading);
  const renderedVariant = normalizeVariant(variant);

  const classNames = cx(
    "ppiq-std-button",
    "ppiq-std-button--" + renderedVariant,
    variant === "action" && "ppiq-std-button--action",
    "ppiq-std-button--" + size,
    disabledState && "ppiq-std-button--is-disabled",
    isLoading && "ppiq-std-button--is-loading",
    fullWidth && "ppiq-std-button--full",
    iconOnly && "ppiq-std-button--icon-only",
    className,
  );

  const readableLabel =
    typeof children === "string" ? children : ariaLabel ?? "Action";

  const content = (
    <>
      {isLoading ? (
        <span className="ppiq-std-button__spinner" aria-hidden="true" />
      ) : (
        leadingIcon
      )}

      {iconOnly ? (
        <span className="sr-only">{ariaLabel ?? readableLabel}</span>
      ) : (
        <span className={isLoading ? "ppiq-std-button__label--loading" : undefined}>
          {isLoading ? loadingLabel : children}
        </span>
      )}

      {!isLoading ? trailingIcon : null}
    </>
  );

  if (as === "a") {
    const anchorProps = rest as Omit<
      AnchorHTMLAttributes<HTMLAnchorElement>,
      "aria-label"
    >;

    return (
      <a
        {...anchorProps}
        ref={ref as Ref<HTMLAnchorElement>}
        className={classNames}
        aria-label={ariaLabel}
        aria-disabled={disabledState || undefined}
        aria-busy={isLoading || undefined}
        data-loading={isLoading ? "true" : undefined}
        data-variant={variant}
        tabIndex={disabledState ? -1 : anchorProps.tabIndex}
        onClick={(event) => {
          if (disabledState) {
            event.preventDefault();
            return;
          }

          const anchorClick = onClick as
            | AnchorHTMLAttributes<HTMLAnchorElement>["onClick"]
            | undefined;

          anchorClick?.(event);
        }}
      >
        {content}
      </a>
    );
  }

  const buttonProps = rest as Omit<
    ButtonHTMLAttributes<HTMLButtonElement>,
    "disabled" | "aria-label"
  >;

  return (
    <button
      {...buttonProps}
      ref={ref as Ref<HTMLButtonElement>}
      type={buttonProps.type ?? "button"}
      className={classNames}
      disabled={disabledState}
      aria-label={ariaLabel}
      aria-busy={isLoading || undefined}
      aria-disabled={disabledState || undefined}
      data-loading={isLoading ? "true" : undefined}
      data-variant={variant}
      onClick={onClick as ButtonHTMLAttributes<HTMLButtonElement>["onClick"]}
    >
      {content}
    </button>
  );
});

StandardButton.displayName = "StandardButton";