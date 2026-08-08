/* PPIQ-T071 */
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import {
  assistantApi,
  type AssistantConfiguration,
  type AssistantContextPayload,
} from "@/api/assistantApi";
import type { Turn } from "@/components/assistant/AssistantChat";

/**
 * THE SINGLE OWNER of the assistant conversation.
 *
 * Chapter 4 5.7.1 specifies a persistent dock on every authenticated page, not
 * a route. Until now the turns and the only askAssistant call lived inside
 * AssistantRuntimePage, so the conversation died the moment the user left
 * /assistant. This provider is mounted by AppLayout ABOVE the router outlet,
 * which is what makes the conversation outlive child-route navigation.
 *
 * NO BROWSER STORAGE. The conversation lives for the lifetime of the
 * authenticated layout and disappears on logout, which is the intended
 * boundary - T-071 is navigation persistence, not cross-login persistence.
 *
 * This file is the ONLY place assistantApi.askAssistant is called. Both an
 * architecture test and the older assistant-chain test assert that.
 */

export const ASSISTANT_CONTEXT_CHIPS = ["grounded", "approved findings"];

export interface AssistantDockValue {
  config: AssistantConfiguration | null;
  turns: Turn[];
  busy: boolean;
  status: string;
  ask: (question: string, context?: AssistantContextPayload | null) => Promise<void>;
  setStatus: (next: string) => void;
  expanded: boolean;
  setExpanded: (next: boolean) => void;
}

const AssistantDockCtx = createContext<AssistantDockValue | null>(null);

export function AssistantDockProvider({ children }: { children: ReactNode }) {
  const [config, setConfig] = useState<AssistantConfiguration | null>(null);
  const [turns, setTurns] = useState<Turn[]>([]);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("Loading assistant runtime configuration...");
  const [expanded, setExpanded] = useState(false);

  /* PPIQ-T071 closure. The provider mounts with the layout, which on a hard
     refresh can happen before the session token has been restored, so ONE
     attempt at mount is not enough: a refused first attempt was never retried
     and the panel kept the failure text for the rest of the session. Ask for
     the configuration when it is needed and remember that it arrived - at
     mount, again the first time the dock is expanded, and again before the
     first question. A refused attempt leaves the flag clear, so the next need
     retries. No storage, no polling, no auth plumbing is read here. */
  const configLoaded = useRef(false);
  const configInFlight = useRef(false);

  const ensureConfig = useCallback(async () => {
    if (configLoaded.current || configInFlight.current) return;
    configInFlight.current = true;
    setStatus("Loading assistant runtime configuration...");
    try {
      const next = await assistantApi.getAssistantConfig();
      configLoaded.current = true;
      setConfig(next);
      setStatus("Assistant runtime is configured.");
    } catch (error) {
      setStatus(
        "Assistant configuration not reachable: " +
          (error instanceof Error ? error.message : String(error)),
      );
    } finally {
      configInFlight.current = false;
    }
  }, []);

  useEffect(() => {
    void ensureConfig();
  }, [ensureConfig]);

  useEffect(() => {
    if (expanded) void ensureConfig();
  }, [expanded, ensureConfig]);

  /* T-072: the envelope is passed IN rather than assembled here. The provider
     stays free of dashboard hooks, so it still mounts anywhere - which is what
     the T-071 persistence test relies on - and the dock, which always renders
     inside the dashboard providers, is the one that knows the surface. */
  const ask = useCallback(
    async (question: string, context?: AssistantContextPayload | null) => {
      setBusy(true);
      setStatus("Asking grounded assistant...");
      setTurns((prev) => [...prev, { role: "user", text: question }]);

      try {
        await ensureConfig();
        const result = await assistantApi.askAssistant(
          question,
          ASSISTANT_CONTEXT_CHIPS,
          config?.allowedTools ?? [],
          context ?? null,
        );
        setTurns((prev) => [...prev, { role: "assistant", answer: result }]);
        setStatus(
          result.isRefusal
            ? "Assistant abstained because evidence was insufficient."
            : "Grounded answer returned with evidence.",
        );
      } catch (error) {
        setTurns((prev) => [
          ...prev,
          {
            role: "assistant",
            error: error instanceof Error ? error.message : String(error),
          },
        ]);
        setStatus("Assistant request failed.");
      } finally {
        setBusy(false);
      }
    },
    [config, ensureConfig],
  );

  const value = useMemo<AssistantDockValue>(
    () => ({ config, turns, busy, status, ask, setStatus, expanded, setExpanded }),
    [config, turns, busy, status, ask, expanded],
  );

  return <AssistantDockCtx.Provider value={value}>{children}</AssistantDockCtx.Provider>;
}

export function useAssistantDock(): AssistantDockValue {
  const value = useContext(AssistantDockCtx);
  if (!value) {
    throw new Error("useAssistantDock must be used inside AssistantDockProvider.");
  }
  return value;
}