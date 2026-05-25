import { useCallback, useState } from "react";
import toast from "@/notifications/toast";

export type CustomerSafeActionOptions<TResult> = {
  actionId: string;
  loadingMessage?: string;
  successMessage?: string;
  errorMessage?: string;
  onSuccess?: (result: TResult) => void | Promise<void>;
  onError?: (error: unknown) => void;
};

function normalizeError(error: unknown) {
  if (error instanceof Error) return error.message;
  return String(error);
}

export function useCustomerSafeAction<TResult = void>(
  action: () => Promise<TResult>,
  options: CustomerSafeActionOptions<TResult>
) {
  const [isRunning, setIsRunning] = useState(false);
  const [lastError, setLastError] = useState<unknown>(null);

  const run = useCallback(async () => {
    setIsRunning(true);
    setLastError(null);

    const toastId = `action:${options.actionId}`;

    if (options.loadingMessage) {
      toast.loading(options.loadingMessage, { id: toastId });
    }

    try {
      const result = await action();

      if (options.successMessage) {
        toast.success(options.successMessage, { id: toastId });
      } else if (options.loadingMessage) {
        toast.dismiss(toastId);
      }

      await options.onSuccess?.(result);
      return result;
    } catch (error) {
      setLastError(error);
      options.onError?.(error);

      toast.error(options.errorMessage ?? "Action failed", {
        id: toastId,
        description: normalizeError(error),
      });

      throw error;
    } finally {
      setIsRunning(false);
    }
  }, [action, options]);

  return {
    run,
    isRunning,
    lastError,
  };
}