import { Toaster } from "sonner";

export function ToastRoot() {
  return (
    <Toaster
      richColors
      closeButton
      position="top-right"
      toastOptions={{
        duration: 4500,
        style: {
          background: "rgba(5, 13, 28, 0.96)",
          border: "1px solid rgba(70, 213, 255, 0.25)",
          color: "#e7f8ff",
        },
      }}
    />
  );
}