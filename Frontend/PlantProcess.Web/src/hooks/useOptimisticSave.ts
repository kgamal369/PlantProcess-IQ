/**
 * hooks/useOptimisticSave.ts
 * --------------------------------------------------------------------
 * Reusable hook that gives every save action the same instant-feedback
 * pattern:
 *
 *   1. Click "Save" → button label changes to "Saving…" with spinner
 *      within ~50ms (no waiting for the network).
 *   2. On success → toast.success("Saved") with auto-dismiss in 2.5s.
 *      Optional onSuccess callback to refresh local state.
 *   3. On failure → toast handled centrally by apiClient; the form
 *      state stays as-is so the user can fix the input and retry.
 *
 * Usage:
 *
 *   const { isSaving, save, error } = useOptimisticSave({
 *     onSave: async () => apiClient.put("/admin/connection-profiles", profile),
 *     successMessage: "Connection profile saved",
 *     onSuccess: (saved) => refreshList(),
 *   });
 *
 *   <button onClick={save} disabled={isSaving}>
 *     {isSaving ? <><Spinner size={14} /> Saving…</> : "Save"}
 *   </button>
 */

import { useCallback, useRef, useState } from "react";
import { toast } from "@/notifications/toast";

export interface UseOptimisticSaveOptions<TResult> {
  /** The async work to perform. Should throw on failure. */
  onSave: () => Promise<TResult>;
  /** Success toast headline. */
  successMessage?: string;
  /** Optional callback when save completes successfully. */
  onSuccess?: (result: TResult) => void;
  /** Optional callback when save fails (after the toast has shown). */
  onError?: (error: unknown) => void;
  /**
   * Unique id used to dedupe toast and to guard against double-submit.
   * Defaults to a hook-instance-stable random id.
   */
  toastId?: string;
}

export interface UseOptimisticSaveReturn {
  isSaving: boolean;
  error: unknown;
  save: () => Promise<void>;
  reset: () => void;
}

export function useOptimisticSave<TResult = unknown>(
  opts: UseOptimisticSaveOptions<TResult>,
): UseOptimisticSaveReturn {
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<unknown>(null);

  // Stable random id per hook instance — used as the toast id so a stuck
  // toast for this form replaces (not stacks) on a retry.
  const idRef = useRef<string>(
    opts.toastId ?? `save-${Math.random().toString(36).slice(2, 10)}`,
  );

  // Prevent double-submit even if the consumer forgets to disable the button.
  const inFlightRef = useRef(false);

  const save = useCallback(async () => {
    if (inFlightRef.current) return;
    inFlightRef.current = true;
    setIsSaving(true);
    setError(null);

    try {
      const result = await opts.onSave();

      if (opts.successMessage) {
        toast.success(opts.successMessage, {
          id: idRef.current,
          durationMs: 2500,
        });
      }
      opts.onSuccess?.(result);
    } catch (err) {
      setError(err);
      opts.onError?.(err);
      // Note: the apiClient already toasted the error — we don't
      // double-toast here. If the consumer wants inline form-level
      // error rendering, the `error` value is available in state.
    } finally {
      setIsSaving(false);
      inFlightRef.current = false;
    }
  }, [opts]);

  const reset = useCallback(() => {
    setError(null);
    setIsSaving(false);
    inFlightRef.current = false;
  }, []);

  return { isSaving, error, save, reset };
}

export default useOptimisticSave;
