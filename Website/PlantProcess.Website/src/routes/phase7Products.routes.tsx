/* PPIQ-PHASE7-PRODUCTS */
import React from "react";
import { ProductPage } from "../pages/products/ProductPage";
import { phase7Products } from "../content/products/index.generated";

// Route objects for data routers (createBrowserRouter).
export const phase7ProductRouteObjects = phase7Products.map((p) => ({
  path: `/products/${p.slug}`,
  element: <ProductPage product={p} />,
}));

// <Route> elements for <Routes> users.
export function Phase7ProductRoutes() {
  // Import Route from your router where you spread this, or use the route objects above.
  // This default export keeps both integration styles available.
  return phase7ProductRouteObjects;
}

// Nav entries for the products menu / overview.
export const phase7NavLinks = phase7Products.map((p) => ({
  label: p.name,
  href: `/products/${p.slug}`,
}));