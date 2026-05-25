import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { AppErrorBoundary } from "@/components/hardening/AppErrorBoundary";
import { ToastRoot } from "@/notifications/ToastRoot";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AppErrorBoundary>
      <BrowserRouter>
        <App />
        <ToastRoot />
      </BrowserRouter>
    </AppErrorBoundary>
  </React.StrictMode>
);