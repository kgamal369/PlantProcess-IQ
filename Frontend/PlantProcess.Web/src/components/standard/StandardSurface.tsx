
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type HTMLAttributes,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { CheckCircle2, Info, Loader2, TriangleAlert, X } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

function cx(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

export type StandardCardElevation = "flat" | "raised" | "floating";

export type StandardCardProps = HTMLAttributes<HTMLElement> & {
  eyebrow?: ReactNode;
  title?: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
  footer?: ReactNode;
  elevation?: StandardCardElevation;
  as?: "section" | "article" | "div";
};

export function StandardCard({
  eyebrow,
  title,
  subtitle,
  actions,
  footer,
  elevation = "raised",
  as = "section",
  children,
  className,
  ...rest
}: StandardCardProps) {
  const Component = as;

  return (
    <Component className={cx("ppiq-std-card", "ppiq-std-card--" + elevation, className)} {...rest}>
      {eyebrow || title || subtitle || actions ? (
        <header className="ppiq-std-card__header">
          <div>
            {eyebrow ? <p className="ppiq-std-card__eyebrow">{eyebrow}</p> : null}
            {title ? <h3 className="ppiq-std-card__title">{title}</h3> : null}
            {subtitle ? <p className="ppiq-std-card__subtitle">{subtitle}</p> : null}
          </div>
          {actions ? <div>{actions}</div> : null}
        </header>
      ) : null}

      <div className="ppiq-std-card__body">{children}</div>

      {footer ? <footer className="ppiq-std-card__footer">{footer}</footer> : null}
    </Component>
  );
}

export type StandardModalSize = "sm" | "md" | "lg" | "xl";

export type StandardModalProps = {
  open: boolean;
  title: ReactNode;
  description?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  size?: StandardModalSize;
  isDirty?: boolean;
  closeOnOutsideClick?: boolean;
  onClose: () => void;
};

const modalWidths: Record<StandardModalSize, string> = {
  sm: "420px",
  md: "640px",
  lg: "840px",
  xl: "1080px",
};

export function StandardModal({
  open,
  title,
  description,
  children,
  footer,
  size = "md",
  isDirty = false,
  closeOnOutsideClick = true,
  onClose,
}: StandardModalProps) {
  const panelRef = useRef<HTMLElement | null>(null);
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    previouslyFocusedRef.current = document.activeElement as HTMLElement | null;

    const focusableSelector =
      "a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex='-1'])";

    const focusFirst = () => {
      const first = panelRef.current?.querySelector<HTMLElement>(focusableSelector);
      first?.focus();
    };

    window.setTimeout(focusFirst, 0);

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }

      if (event.key === "Tab") {
        const focusable = Array.from(panelRef.current?.querySelectorAll<HTMLElement>(focusableSelector) ?? []);
        if (focusable.length === 0) return;

        const first = focusable[0];
        const last = focusable[focusable.length - 1];

        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first.focus();
        }
      }
    };

    document.addEventListener("keydown", onKeyDown);

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      previouslyFocusedRef.current?.focus?.();
    };
  }, [open, onClose]);

  if (!open || typeof document === "undefined") {
    return null;
  }

  return createPortal(
    <div
      className="ppiq-std-modal-backdrop"
      role="presentation"
      onMouseDown={() => {
        if (closeOnOutsideClick && !isDirty) onClose();
      }}
    >
      <section
        ref={panelRef}
        className="ppiq-std-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="ppiq-standard-modal-title"
        style={{ "--ppiq-modal-width": modalWidths[size] } as CSSProperties}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header className="ppiq-std-modal__header">
          <div>
            <h2 id="ppiq-standard-modal-title" style={{ margin: 0 }}>
              {title}
            </h2>
            {description ? (
              <p style={{ margin: "6px 0 0", color: "var(--ppiq-std-text-soft)" }}>{description}</p>
            ) : null}
          </div>

          <StandardButton variant="ghost" size="sm" iconOnly ariaLabel="Close modal" onClick={onClose}>
            <X size={18} />
          </StandardButton>
        </header>

        <div className="ppiq-std-modal__body">{children}</div>

        {footer ? (
          <footer className="ppiq-std-modal__footer">
            <div />
            <div className="ppiq-std-modal__footer-actions">{footer}</div>
          </footer>
        ) : null}
      </section>
    </div>,
    document.body,
  );
}

export type StandardToastVariant = "info" | "success" | "warning" | "error" | "loading";

export type StandardToastMessage = {
  id: string;
  variant: StandardToastVariant;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  durationMs?: number;
};

type ToastContextValue = {
  notify: (message: Omit<StandardToastMessage, "id"> & { id?: string }) => string;
  dismiss: (id: string) => void;
};

const ToastContext = createContext<ToastContextValue | null>(null);

const toastIcons: Record<StandardToastVariant, ReactNode> = {
  info: <Info size={18} />,
  success: <CheckCircle2 size={18} />,
  warning: <TriangleAlert size={18} />,
  error: <TriangleAlert size={18} />,
  loading: <Loader2 size={18} className="ppiq-std-button__spinner" />,
};

export function StandardToastProvider({ children }: { children: ReactNode }) {
  const [messages, setMessages] = useState<StandardToastMessage[]>([]);

  const dismiss = useCallback((id: string) => {
    setMessages((current) => current.filter((message) => message.id !== id));
  }, []);

  const notify = useCallback(
    (message: Omit<StandardToastMessage, "id"> & { id?: string }) => {
      const id = message.id ?? "toast-" + Date.now() + "-" + Math.random().toString(36).slice(2);
      const next: StandardToastMessage = { ...message, id };

      setMessages((current) => [next, ...current].slice(0, 5));

      if (next.variant !== "loading") {
        window.setTimeout(() => dismiss(id), next.durationMs ?? 5000);
      }

      return id;
    },
    [dismiss],
  );

  return (
    <ToastContext.Provider value={{ notify, dismiss }}>
      {children}
      <div className="ppiq-toast-viewport" role="region" aria-label="Notifications">
        {messages.slice(0, 3).map((message) => (
          <article key={message.id} className="ppiq-toast" role="status">
            <div style={{ display: "flex", gap: 10 }}>
              <span aria-hidden="true">{toastIcons[message.variant]}</span>
              <div>
                <p className="ppiq-toast__title">{message.title}</p>
                {message.description ? <p className="ppiq-toast__description">{message.description}</p> : null}
                {message.action ? <div style={{ marginTop: 8 }}>{message.action}</div> : null}
              </div>
            </div>
            <StandardButton variant="ghost" size="sm" iconOnly ariaLabel="Dismiss notification" onClick={() => dismiss(message.id)}>
              <X size={14} />
            </StandardButton>
          </article>
        ))}

        {messages.length > 3 ? (
          <article className="ppiq-toast" role="status">
            <p className="ppiq-toast__title">+{messages.length - 3} more notifications</p>
          </article>
        ) : null}
      </div>
    </ToastContext.Provider>
  );
}

export function useStandardToast() {
  const value = useContext(ToastContext);
  if (!value) {
    throw new Error("useStandardToast must be used inside StandardToastProvider.");
  }
  return value;
}
