
import {
  forwardRef,
  useMemo,
  useState,
  type ChangeEvent,
  type InputHTMLAttributes,
  type ReactNode,
  type TextareaHTMLAttributes,
} from "react";
import { ChevronDown, Search, X } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

export type StandardFieldSize = "sm" | "md" | "lg";

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

type FieldChromeProps = {
  id?: string;
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  size?: StandardFieldSize;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  children: (id: string, describedBy?: string) => ReactNode;
  textarea?: boolean;
  className?: string;
};

function FieldChrome({
  id,
  label,
  helperText,
  error,
  required,
  size = "md",
  leadingIcon,
  trailingIcon,
  children,
  textarea,
  className,
}: FieldChromeProps) {
  const safeId = id ?? "ppiq-field-" + Math.random().toString(36).slice(2);
  const hintId = helperText ? safeId + "-hint" : undefined;
  const errorId = error ? safeId + "-error" : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className={cx("ppiq-std-field", Boolean(error) && "ppiq-std-field--error", className)}>
      {label ? (
        <label className="ppiq-std-field__label" htmlFor={safeId}>
          {label}
          {required ? <span className="ppiq-std-field__required">*</span> : null}
        </label>
      ) : null}

      <div className={cx("ppiq-std-field__shell", "ppiq-std-field__shell--" + size, textarea && "ppiq-std-field__textarea-shell")}>
        {leadingIcon ? <span aria-hidden="true">{leadingIcon}</span> : null}
        {children(safeId, describedBy)}
        {trailingIcon ? <span aria-hidden="true">{trailingIcon}</span> : null}
      </div>

      {error ? (
        <div id={errorId} role="alert" className="ppiq-std-field__error">
          {error}
        </div>
      ) : (
        <div id={hintId} className="ppiq-std-field__helper">
          {helperText ?? " "}
        </div>
      )}
    </div>
  );
}

export type StandardInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "size" | "onChange"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  size?: StandardFieldSize;
  isLoading?: boolean;
  value?: string;
  onChange?: (value: string, event: ChangeEvent<HTMLInputElement>) => void;
};

export const StandardInput = forwardRef<HTMLInputElement, StandardInputProps>(
  (
    {
      label,
      helperText,
      error,
      leadingIcon,
      trailingIcon,
      size = "md",
      required,
      id,
      type = "text",
      value,
      onChange,
      className,
      disabled,
      isLoading,
      ...rest
    },
    ref,
  ) => {
    const isSearch = type === "search";

    return (
      <FieldChrome
        id={id}
        label={label}
        helperText={helperText}
        error={error}
        required={required}
        size={size}
        leadingIcon={leadingIcon ?? (isSearch ? <Search size={16} /> : undefined)}
        trailingIcon={
          trailingIcon ??
          (isSearch && value ? (
            <StandardButton
              variant="ghost"
              size="sm"
              iconOnly
              ariaLabel="Clear search"
              onClick={() => {
                const synthetic = { target: { value: "" } } as ChangeEvent<HTMLInputElement>;
                onChange?.("", synthetic);
              }}
            >
              <X size={14} />
            </StandardButton>
          ) : undefined)
        }
        className={className}
      >
        {(fieldId, describedBy) => (
          <input
            ref={ref}
            id={fieldId}
            className="ppiq-std-field__control"
            type={type}
            value={value}
            required={required}
            disabled={disabled || isLoading}
            aria-invalid={Boolean(error) || undefined}
            aria-describedby={describedBy}
            aria-busy={isLoading || undefined}
            onChange={(event) => onChange?.(event.target.value, event)}
            {...rest}
          />
        )}
      </FieldChrome>
    );
  },
);

StandardInput.displayName = "StandardInput";

export type StandardTextAreaProps = Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "onChange"> & {
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  size?: StandardFieldSize;
  value?: string;
  onChange?: (value: string, event: ChangeEvent<HTMLTextAreaElement>) => void;
};

export const StandardTextArea = forwardRef<HTMLTextAreaElement, StandardTextAreaProps>(
  (
    {
      label,
      helperText,
      error,
      leadingIcon,
      trailingIcon,
      size = "md",
      required,
      id,
      value,
      onChange,
      className,
      ...rest
    },
    ref,
  ) => (
    <FieldChrome
      id={id}
      label={label}
      helperText={helperText}
      error={error}
      required={required}
      size={size}
      leadingIcon={leadingIcon}
      trailingIcon={trailingIcon}
      textarea
      className={className}
    >
      {(fieldId, describedBy) => (
        <textarea
          ref={ref}
          id={fieldId}
          className="ppiq-std-field__control ppiq-std-field__textarea"
          value={value}
          required={required}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={describedBy}
          onChange={(event) => onChange?.(event.target.value, event)}
          {...rest}
        />
      )}
    </FieldChrome>
  ),
);

StandardTextArea.displayName = "StandardTextArea";

export type StandardSelectOption = {
  value: string;
  label: ReactNode;
  searchText?: string;
  disabled?: boolean;
};

export type StandardSelectProps = {
  id?: string;
  label?: ReactNode;
  helperText?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  disabled?: boolean;
  size?: StandardFieldSize;
  placeholder?: string;
  value?: string | string[];
  options: ReadonlyArray<StandardSelectOption>;
  multiple?: boolean;
  searchable?: boolean;
  onChange?: (value: string | string[]) => void;
};

export function StandardSelect({
  id,
  label,
  helperText,
  error,
  required,
  disabled,
  size = "md",
  placeholder = "Select...",
  value,
  options,
  multiple = false,
  searchable = false,
  onChange,
}: StandardSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");

  const selectedValues = Array.isArray(value) ? value : value ? [value] : [];

  const filteredOptions = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((option) =>
      String(option.searchText ?? option.label).toLowerCase().includes(q),
    );
  }, [options, query]);

  const selectedLabels = options
    .filter((option) => selectedValues.includes(option.value))
    .map((option) => option.label);

  function toggle(valueToToggle: string) {
    if (multiple) {
      const next = selectedValues.includes(valueToToggle)
        ? selectedValues.filter((item) => item !== valueToToggle)
        : [...selectedValues, valueToToggle];
      onChange?.(next);
      return;
    }

    onChange?.(valueToToggle);
    setOpen(false);
  }

  return (
    <FieldChrome
      id={id}
      label={label}
      helperText={helperText}
      error={error}
      required={required}
      size={size}
      trailingIcon={<ChevronDown size={16} />}
    >
      {(fieldId, describedBy) => (
        <div style={{ width: "100%", position: "relative" }}>
          <button
            id={fieldId}
            type="button"
            className="ppiq-std-field__control"
            disabled={disabled}
            aria-haspopup="listbox"
            aria-expanded={open}
            aria-invalid={Boolean(error) || undefined}
            aria-describedby={describedBy}
            onClick={() => setOpen((current) => !current)}
            onKeyDown={(event) => {
              if (event.key === "Escape") setOpen(false);
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                setOpen((current) => !current);
              }
            }}
            style={{ textAlign: "left" }}
          >
            {selectedLabels.length > 0 ? (
              multiple ? (
                <span className="ppiq-std-chip-list">
                  {selectedLabels.map((label, index) => (
                    <span className="ppiq-std-chip" key={index}>
                      {label}
                    </span>
                  ))}
                </span>
              ) : (
                selectedLabels[0]
              )
            ) : (
              <span style={{ color: "rgba(160, 189, 216, 0.52)" }}>{placeholder}</span>
            )}
          </button>

          {open ? (
            <div className="ppiq-std-select-menu" role="listbox" aria-multiselectable={multiple || undefined}>
              {searchable ? (
                <input
                  className="ppiq-std-field__control"
                  placeholder="Search options..."
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Escape") setOpen(false);
                  }}
                  style={{ padding: "8px 10px", borderBottom: "1px solid rgba(0, 212, 255, 0.12)" }}
                />
              ) : null}

              {filteredOptions.map((option) => (
                <div
                  key={option.value}
                  role="option"
                  aria-selected={selectedValues.includes(option.value)}
                  className="ppiq-std-select-option"
                  onClick={() => {
                    if (!option.disabled) toggle(option.value);
                  }}
                >
                  {option.label}
                </div>
              ))}
            </div>
          ) : null}
        </div>
      )}
    </FieldChrome>
  );
}
