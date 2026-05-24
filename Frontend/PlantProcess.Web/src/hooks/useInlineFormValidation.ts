import { useCallback, useMemo, useState } from "react";

export type FieldErrors<T extends string> = Partial<Record<T, string>>;

export type Validator<TForm, TField extends string> = (
  form: TForm
) => FieldErrors<TField>;

export function useInlineFormValidation<TForm, TField extends string>(
  form: TForm,
  validator: Validator<TForm, TField>
) {
  const [touched, setTouched] = useState<Partial<Record<TField, boolean>>>({});
  const [submitAttempted, setSubmitAttempted] = useState(false);

  const errors = useMemo(() => validator(form), [form, validator]);

  const hasErrors = Object.values(errors).some(Boolean);

  const markTouched = useCallback((field: TField) => {
    setTouched((current) => ({ ...current, [field]: true }));
  }, []);

  const shouldShow = useCallback(
    (field: TField) => Boolean(touched[field] || submitAttempted),
    [submitAttempted, touched]
  );

  const getError = useCallback(
    (field: TField) => (shouldShow(field) ? errors[field] : undefined),
    [errors, shouldShow]
  );

  const prepareSubmit = useCallback(() => {
    setSubmitAttempted(true);
    return !hasErrors;
  }, [hasErrors]);

  const resetValidation = useCallback(() => {
    setTouched({});
    setSubmitAttempted(false);
  }, []);

  return {
    errors,
    hasErrors,
    markTouched,
    getError,
    prepareSubmit,
    resetValidation,
  };
}

export function validateRequired(value: unknown, label: string) {
  return typeof value === "string" && value.trim().length > 0
    ? undefined
    : `${label} is required.`;
}

export function validatePort(value: unknown, label = "Port") {
  const parsed = typeof value === "number" ? value : Number(value);

  if (!Number.isInteger(parsed) || parsed < 1 || parsed > 65535) {
    return `${label} must be an integer between 1 and 65535.`;
  }

  return undefined;
}

export function validateIntervalMinutes(
  value: unknown,
  minimum: number,
  label = "Refresh interval"
) {
  const parsed = typeof value === "number" ? value : Number(value);

  if (!Number.isInteger(parsed) || parsed < minimum) {
    return `${label} must be at least ${minimum} minutes for the current license.`;
  }

  return undefined;
}

export function validateCode(value: unknown, label = "Code") {
  const text = typeof value === "string" ? value.trim() : "";

  if (!text) return `${label} is required.`;

  if (!/^[a-zA-Z][a-zA-Z0-9_-]{2,63}$/.test(text)) {
    return `${label} must start with a letter and contain 3–64 letters, numbers, underscores or hyphens.`;
  }

  return undefined;
}