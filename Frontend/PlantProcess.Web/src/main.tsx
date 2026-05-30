import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { ErrorBoundary } from "@/components/standard/ErrorBoundary";
import { ToastRoot } from "@/notifications/ToastRoot";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ErrorBoundary routePath="app-root" fallbackTitle="The application shell is refreshing">
      <BrowserRouter>
        <App />
        <ToastRoot />
      </BrowserRouter>
    </ErrorBoundary>
  </React.StrictMode>,
);
