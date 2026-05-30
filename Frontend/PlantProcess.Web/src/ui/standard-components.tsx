import {
  createContext,
  forwardRef,
  useCallback,
  useContext,
  useEffect,
  useId,
  useMemo,
  useState,
  type ButtonHTMLAttributes,
  type HTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";
import { createPortal } from "react-dom";
import "./design-tokens.css";
import "./standard-components.css";

export type StandardSize = "xs" | "sm" | "md" | "lg";
export type StandardButtonVariant = "primary" | "secondary" | "ghost" | "danger" | "success";

export function cx(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}

export type StandardButtonProps = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "type"> & {
  variant?: StandardButtonVariant;
  size?: StandardSize;
  fullWidth?: boolean;
  loading?: boolean;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
  type?: "button" | "submit" | "reset";
};

export const StandardButton = forwardRef<HTMLButtonElement, StandardButtonProps>(
  (
    {
      variant = "primary",
      size = "md",
      fullWidth = false,
      loading = false,
      leftIcon,
      rightIcon,
      children,
      className,
      disabled,
      type = "button",
      ...rest
    },
    ref,
  ) => {
    const isDisabled = disabled || loading;

    return (
      <button
        ref={ref}
        type={type}
        className={cx(
          "ppiq-btn",
          `ppiq-btn--${variant}`,
          `ppiq-btn--${size}`,
          fullWidth && "ppiq-btn--full",
          className,
        )}
        disabled={isDisabled}
        aria-busy={loading || undefined}
        aria-disabled={isDisabled || undefined}
        {...rest}
      >
        {loading ? <span className="ppiq-spinner" aria-hidden="true" /> : null}
        {!loading && leftIcon ? <span>{leftIcon}</span> : null}
        <span>{children}</span>
        {!loading && rightIcon ? <span>{rightIcon}</span> : null}
      </button>
    );
  },
);

StandardButton.displayName = "StandardButton";

export type StandardCardProps = HTMLAttributes<HTMLElement> & {
  eyebrow?: ReactNode;
  title?: ReactNode;
  subtitle?: ReactNode;
  footer?: ReactNode;
  as?: "section" | "article" | "div";
};

export function StandardCard({
  eyebrow,
  title,
  subtitle,
  footer,
  children,
  className,
  as = "section",
  ...rest
}: StandardCardProps) {
  const Component = as;

  return (
    <Component className={cx("ppiq-card", className)} {...rest}>
      {eyebrow || title || subtitle ? (
        <header className="ppiq-card__header">
          {eyebrow ? <p className="ppiq-card__eyebrow">{eyebrow}</p> : null}
          {title ? <h3 className="ppiq-card__title">{title}</h3> : null}
          {subtitle ? <p className="ppiq-card__subtitle">{subtitle}</p> : null}
        </header>
      ) : null}
      <div className="ppiq-card__body">{children}</div>
      {footer ? <footer className="ppiq-card__footer">{footer}</footer> : null}
    </Component>
  );
}

export type StandardTableAlignment = "left" | "center" | "right";
export type StandardTableTone = "neutral" | "success" | "warning" | "danger";

export type StandardTableColumn<T> = {
  key: string;
  header: ReactNode;
  cell: (row: T, rowIndex: number) => ReactNode;
  align?: StandardTableAlignment;
  width?: string;
};

export type StandardTableProps<T> = {
  caption?: string;
  columns: ReadonlyArray<StandardTableColumn<T>>;
  data: ReadonlyArray<T>;
  getRowKey: (row: T, rowIndex: number) => string | number;
  loading?: boolean;
  error?: ReactNode;
  emptyTitle?: ReactNode;
  emptyDescription?: ReactNode;
  onRowClick?: (row: T, rowIndex: number) => void;
  getRowTone?: (row: T, rowIndex: number) => StandardTableTone;
  className?: string;
};

export function StandardTable<T>({
  caption,
  columns,
  data,
  getRowKey,
  loading = false,
  error,
  emptyTitle = "No records available",
  emptyDescription = "Adjust filters or refresh the data source.",
  onRowClick,
  getRowTone,
  className,
}: StandardTableProps<T>) {
  if (error) {
    return (
      <div className={cx("ppiq-table-wrap", className)}>
        <div className="ppiq-state">
          <strong>Table refresh failed</strong>
          <span>{error}</span>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className={cx("ppiq-table-wrap", className)}>
        <div className="ppiq-state">
          <strong>Refreshing table</strong>
          <span>Loading the latest manufacturing intelligence data.</span>
        </div>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className={cx("ppiq-table-wrap", className)}>
        <div className="ppiq-state">
          <strong>{emptyTitle}</strong>
          <span>{emptyDescription}</span>
        </div>
      </div>
    );
  }

  return (
    <div className={cx("ppiq-table-wrap", className)}>
      <div className="ppiq-table-scroll">
        <table className="ppiq-table">
          {caption ? <caption>{caption}</caption> : null}
          <thead>
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={cx(
                    column.align === "right" && "ppiq-align-right",
                    column.align === "center" && "ppiq-align-center",
                  )}
                  style={column.width ? { width: column.width } : undefined}
                >
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.map((row, rowIndex) => {
              const tone = getRowTone?.(row, rowIndex) ?? "neutral";

              return (
                <tr
                  key={getRowKey(row, rowIndex)}
                  className={cx(
                    onRowClick && "ppiq-clickable-row",
                    tone !== "neutral" && `ppiq-row-${tone}`,
                  )}
                  onClick={onRowClick ? () => onRowClick(row, rowIndex) : undefined}
                >
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={cx(
                        column.align === "right" && "ppiq-align-right",
                        column.align === "center" && "ppiq-align-center",
                      )}
                    >
                      {column.cell(row, rowIndex)}
                    </td>
                  ))}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export type StandardTabItem<TId extends string = string> = {
  id: TId;
  label: ReactNode;
  badge?: ReactNode;
  disabled?: boolean;
  panel?: ReactNode;
};

export type StandardTabsProps<TId extends string = string> = {
  items: ReadonlyArray<StandardTabItem<TId>>;
  activeId: TId;
  onChange: (id: TId) => void;
  ariaLabel: string;
  children?: ReactNode;
  className?: string;
};

export function StandardTabs<TId extends string = string>({
  items,
  activeId,
  onChange,
  ariaLabel,
  children,
  className,
}: StandardTabsProps<TId>) {
  const active = items.find((item) => item.id === activeId);

  return (
    <div className={cx("ppiq-tabs", className)}>
      <div className="ppiq-tabs__list" role="tablist" aria-label={ariaLabel}>
        {items.map((item) => (
          <button
            key={item.id}
            type="button"
            role="tab"
            className="ppiq-tabs__button"
            aria-selected={item.id === activeId}
            disabled={item.disabled}
            onClick={() => onChange(item.id)}
          >
            <span>{item.label}</span>
            {item.badge ? <span>{item.badge}</span> : null}
          </button>
        ))}
      </div>
      <div role="tabpanel">{children ?? active?.panel}</div>
    </div>
  );
}

type FieldShellProps = {
  id?: string;
  label?: ReactNode;
  required?: boolean;
  helperText?: ReactNode;
  error?: ReactNode;
  className?: string;
  children: (id: string, describedBy?: string) => ReactNode;
};

function FieldShell({ id, label, required, helperText, error, className, children }: FieldShellProps) {
  const generatedId = useId();
  const fieldId = id ?? generatedId;
  const hintId = helperText ? `${fieldId}-hint` : undefined;
  const errorId = error ? `${fieldId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className={cx("ppiq-field", Boolean(error) && "ppiq-field--error", className)}>
      {label ? (
        <label className="ppiq-field__label" htmlFor={fieldId}>
          {label}
          {required ? <span className="ppiq-field__required">*</span> : null}
        </label>
      ) : null}
      {children(fieldId, describedBy)}
      {helperText ? (
        <div className="ppiq-field__hint" id={hintId}>
          {helperText}
        </div>
      ) : null}
      {error ? (
        <div className="ppiq-field__error" id={errorId} role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}

export type StandardInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "size"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
};

export const StandardInput = forwardRef<HTMLInputElement, StandardInputProps>(
  ({ label, helperText, error, className, required, id, ...rest }, ref) => (
    <FieldShell id={id} label={label} required={required} helperText={helperText} error={error} className={className}>
      {(fieldId, describedBy) => (
        <input
          ref={ref}
          id={fieldId}
          className="ppiq-field__control"
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          {...rest}
        />
      )}
    </FieldShell>
  ),
);

StandardInput.displayName = "StandardInput";

export type StandardSelectOption = {
  value: string;
  label: ReactNode;
  disabled?: boolean;
};

export type StandardSelectProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, "size"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  placeholder?: string;
  options: ReadonlyArray<StandardSelectOption>;
};

export const StandardSelect = forwardRef<HTMLSelectElement, StandardSelectProps>(
  ({ label, helperText, error, placeholder, options, className, required, id, ...rest }, ref) => (
    <FieldShell id={id} label={label} required={required} helperText={helperText} error={error} className={className}>
      {(fieldId, describedBy) => (
        <select
          ref={ref}
          id={fieldId}
          className="ppiq-field__control"
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          {...rest}
        >
          {placeholder ? <option value="">{placeholder}</option> : null}
          {options.map((option) => (
            <option key={option.value} value={option.value} disabled={option.disabled}>
              {option.label}
            </option>
          ))}
        </select>
      )}
    </FieldShell>
  ),
);

StandardSelect.displayName = "StandardSelect";

export type StandardTextAreaProps = TextareaHTMLAttributes<HTMLTextAreaElement> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
};

export const StandardTextArea = forwardRef<HTMLTextAreaElement, StandardTextAreaProps>(
  ({ label, helperText, error, className, required, id, ...rest }, ref) => (
    <FieldShell id={id} label={label} required={required} helperText={helperText} error={error} className={className}>
      {(fieldId, describedBy) => (
        <textarea
          ref={ref}
          id={fieldId}
          className="ppiq-field__control ppiq-field__textarea"
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          {...rest}
        />
      )}
    </FieldShell>
  ),
);

StandardTextArea.displayName = "StandardTextArea";

export type StandardModalProps = {
  open: boolean;
  title: ReactNode;
  description?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
};

export function StandardModal({ open, title, description, children, footer, onClose }: StandardModalProps) {
  useEffect(() => {
    if (!open) return;

    const handler = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };

    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open, onClose]);

  if (!open || typeof document === "undefined") return null;

  return createPortal(
    <div className="ppiq-modal-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="ppiq-modal" role="dialog" aria-modal="true" onMouseDown={(event) => event.stopPropagation()}>
        <header className="ppiq-modal__header">
          <div>
            <h2>{title}</h2>
            {description ? <p>{description}</p> : null}
          </div>
          <StandardButton variant="ghost" size="sm" onClick={onClose} aria-label="Close modal">
            Ã—
          </StandardButton>
        </header>
        <div className="ppiq-modal__body">{children}</div>
        {footer ? <footer className="ppiq-modal__footer">{footer}</footer> : null}
      </section>
    </div>,
    document.body,
  );
}

export type StandardToastMessage = {
  id: string;
  intent: "success" | "warning" | "danger" | "info";
  title: ReactNode;
  description?: ReactNode;
};

type ToastContextValue = {
  notify: (message: Omit<StandardToastMessage, "id"> & { id?: string }) => string;
  dismiss: (id: string) => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

export function StandardToastProvider({ children }: { children: ReactNode }) {
  const [messages, setMessages] = useState<StandardToastMessage[]>([]);

  const dismiss = useCallback((id: string) => {
    setMessages((current) => current.filter((message) => message.id !== id));
  }, []);

  const notify = useCallback(
    (message: Omit<StandardToastMessage, "id"> & { id?: string }) => {
      const id = message.id ?? `toast-${Date.now()}-${Math.random().toString(36).slice(2)}`;
      setMessages((current) => [{ ...message, id }, ...current].slice(0, 5));
      window.setTimeout(() => dismiss(id), 6000);
      return id;
    },
    [dismiss],
  );

  const value = useMemo(() => ({ notify, dismiss }), [notify, dismiss]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="ppiq-toast-viewport" role="region" aria-label="Notifications">
        {messages.map((message) => (
          <article key={message.id} className="ppiq-toast" role="status">
            <div>
              <p className="ppiq-toast__title">{message.title}</p>
              {message.description ? <p className="ppiq-toast__description">{message.description}</p> : null}
            </div>
            <StandardButton variant="ghost" size="xs" onClick={() => dismiss(message.id)} aria-label="Dismiss notification">
              Ã—
            </StandardButton>
          </article>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useStandardToast() {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useStandardToast must be used inside StandardToastProvider.");
  return context;
}