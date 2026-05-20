import { Bell, CircleUserRound, DatabaseZap, ShieldCheck } from "lucide-react";
import { SOUBrand } from "./SOUBrand";
import { ProductBrand } from "./ProductBrand";
import "./app-command-header.css";

type AppCommandHeaderProps = {
  licenseTier?: "Light" | "Pro" | "Pro Plus" | "Enterprise" | "Demo";
  environment?: "Demo" | "Development" | "Staging" | "Production";
  plantName?: string;
  userName?: string;
};

export function AppCommandHeader({
  licenseTier = "Demo",
  environment = "Demo",
  plantName = "Demo Plant",
  userName = "Admin",
}: AppCommandHeaderProps) {
  return (
    <header className="app-command-header">
      <div className="app-command-header__glow" aria-hidden="true" />

      <div className="app-command-header__left">
        <div className="app-command-header__company">
          <SOUBrand compact />
        </div>

        <span className="app-command-header__divider" aria-hidden="true" />

        <ProductBrand />
      </div>

      <div className="app-command-header__center" aria-label="PlantProcess IQ runtime context">
        <div className="app-command-header__context-card">
          <DatabaseZap size={16} aria-hidden="true" />
          <span className="app-command-header__context-label">Plant</span>
          <strong>{plantName}</strong>
        </div>

        <div className="app-command-header__context-card app-command-header__context-card--status">
          <ShieldCheck size={16} aria-hidden="true" />
          <span className="app-command-header__context-label">Status</span>
          <strong>Healthy</strong>
        </div>
      </div>

      <div className="app-command-header__right">
        <span className={`app-command-header__env app-command-header__env--${environment.toLowerCase()}`}>
          {environment}
        </span>

        <span className={`app-command-header__license app-command-header__license--${licenseTier.toLowerCase().replaceAll(" ", "-")}`}>
          {licenseTier}
        </span>

        <button className="app-command-header__icon-button" type="button" aria-label="Notifications">
          <Bell size={17} aria-hidden="true" />
          <span className="app-command-header__notification-dot" aria-hidden="true" />
        </button>

        <button className="app-command-header__user" type="button" aria-label="User menu">
          <CircleUserRound size={18} aria-hidden="true" />
          <span>{userName}</span>
        </button>
      </div>
    </header>
  );
}
