/**
 * notifications/toast.ts
 * --------------------------------------------------------------------
 * Thin wrapper around sonner's toast API with brand-friendly defaults
 * and structured methods so every caller is consistent.
 *
 * Why not use sonner directly?
 *   - We dedupe by id so the same API failure never stacks 5 toasts
 *   - We force durations to readable values
 *   - We expose semantic helpers (error/success/warning/info/loading)
 *   - We centralise the place to change all toast behaviour later
 *
 * Sonner must be installed:  npm install sonner
 */

import { toast as sonnerToast } from "sonner";

/** Common toast options accepted by every helper. */
export interface ToastOptions {
  /**
   * Stable id used for dedupe and to update an existing toast.
   * If the same id fires twice while the first is still visible,
   * the second REPLACES the first instead of stacking.
   */
  id?: string;
  /** Optional secondary line below the headline. */
  description?: string;
  /** Override duration in ms. Defaults below per kind. */
  durationMs?: number;
  /** Optional action button (label + onClick). */
  action?: { label: string; onClick: () => void };
}

const DEFAULTS = {
  error:   { duration: 6000 },
  warning: { duration: 5000 },
  success: { duration: 2500 },
  info:    { duration: 4000 },
  loading: { duration: Infinity },
};

export const toast = {
  error(message: string, opts: ToastOptions = {}) {
    return sonnerToast.error(message, {
      id: opts.id,
      description: opts.description,
      duration: opts.durationMs ?? DEFAULTS.error.duration,
      action: opts.action,
    });
  },

  warning(message: string, opts: ToastOptions = {}) {
    return sonnerToast.warning(message, {
      id: opts.id,
      description: opts.description,
      duration: opts.durationMs ?? DEFAULTS.warning.duration,
      action: opts.action,
    });
  },

  success(message: string, opts: ToastOptions = {}) {
    return sonnerToast.success(message, {
      id: opts.id,
      description: opts.description,
      duration: opts.durationMs ?? DEFAULTS.success.duration,
      action: opts.action,
    });
  },

  info(message: string, opts: ToastOptions = {}) {
    return sonnerToast(message, {
      id: opts.id,
      description: opts.description,
      duration: opts.durationMs ?? DEFAULTS.info.duration,
      action: opts.action,
    });
  },

  /**
   * Loading toast — returns the toast id so the caller can dismiss
   * or replace it when the async work completes.
   *
   *   const id = toast.loading("Saving…", { id: "save-profile" });
   *   await api.savePofile();
   *   toast.success("Saved", { id });
   */
  loading(message: string, opts: ToastOptions = {}) {
    return sonnerToast.loading(message, {
      id: opts.id,
      description: opts.description,
      duration: opts.durationMs ?? DEFAULTS.loading.duration,
    });
  },

  /** Dismiss a toast by id (or all toasts if id is omitted). */
  dismiss(id?: string) {
    sonnerToast.dismiss(id);
  },
};

export default toast;
