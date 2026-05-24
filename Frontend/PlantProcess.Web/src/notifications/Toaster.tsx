/**
 * notifications/Toaster.tsx
 * --------------------------------------------------------------------
 * Mount once at the top of AppLayout. Renders the brand-themed
 * sonner Toaster with appropriate position, colors, and dismiss UX.
 *
 * Usage in AppLayout.tsx:
 *   import { AppToaster } from "@/notifications/Toaster";
 *   ...
 *   return (
 *     <>
 *       <AppToaster />
 *       <Outlet />
 *     </>
 *   );
 */

import { Toaster as SonnerToaster } from "sonner";

export function AppToaster() {
  return (
    <SonnerToaster
      position="top-right"
      richColors
      closeButton
      expand={false}
      visibleToasts={4}
      toastOptions={{
        // Brand-consistent dark navy with cyan accent border
        style: {
          background: "#0f172a",
          color: "#eaf6ff",
          border: "1px solid #0891b2",
          fontFamily:
            'Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
          fontSize: "0.9rem",
          boxShadow: "0 8px 28px rgba(0, 0, 0, 0.3)",
        },
        classNames: {
          // Slightly higher contrast for the description line
          description: "ppiq-toast-description",
          actionButton: "ppiq-toast-action",
          cancelButton: "ppiq-toast-cancel",
        },
      }}
    />
  );
}

export default AppToaster;
