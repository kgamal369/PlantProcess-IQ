// ============================================================
// FILE: Frontend/PlantProcess.Web/src/notifications/AppToastHost.tsx
//
// Purpose:
//   Single global Sonner toast host.
//
// Why:
//   E2E currently sees unstable [data-sonner-toaster] count.
//   The app must mount exactly one <Toaster /> at the root level.
// ============================================================

import { Toaster } from "sonner";

export function AppToastHost() {
  return (
    <Toaster
      position="top-right"
      richColors
      closeButton
      toastOptions={{
        duration: 4500,
      }}
    />
  );
}

export default AppToastHost;