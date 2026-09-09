// PPIQ T-254. The DB configuration surface renders the backend's connector truth.
//
// Two things are proved, and they are the two that matter: what the page says about a
// provider comes from the server, and a server that does not answer produces an empty
// list rather than a remembered one.

import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const getConnectionProfiles = vi.fn();
const getConnectorProviderTypes = vi.fn();

vi.mock("@/api/productApiClient", () => ({
  productApi: {
    getConnectionProfiles: (...a: unknown[]) => getConnectionProfiles(...a),
    getConnectorProviderTypes: (...a: unknown[]) => getConnectorProviderTypes(...a),
  },
}));

const { DbConfigurationTab } = await import("./AdminDbConfigurationTab");

const BACKEND_PROVIDERS = [
  {
    providerType: "PostgreSql",
    displayName: "PostgreSQL Read-only DB Link",
    description: "Read-only DB link to PostgreSQL source systems.",
    isAvailableNow: true,
    requiresSecretReference: true,
    supportsSchemaDiscovery: true,
    supportsSnapshotImport: true,
    supportsIncrementalImport: true,
  },
  {
    providerType: "Sap",
    displayName: "SAP",
    description: "Read-only connector for SAP source systems.",
    isAvailableNow: false,
    requiresSecretReference: true,
    supportsSchemaDiscovery: true,
    supportsSnapshotImport: true,
    supportsIncrementalImport: true,
  },
];

beforeEach(() => {
  getConnectionProfiles.mockReset();
  getConnectorProviderTypes.mockReset();
  getConnectionProfiles.mockResolvedValue([]);
  getConnectorProviderTypes.mockResolvedValue(BACKEND_PROVIDERS);
});

describe("connector truth at the DB configuration surface", () => {
  it("describes a provider in the server's words, not its own", async () => {
    render(<DbConfigurationTab data={null} onRefresh={() => {}} />);

    expect(await screen.findByText("Read-only DB link to PostgreSQL source systems.")).toBeTruthy();
    expect(screen.getByText("Read-only connector for SAP source systems.")).toBeTruthy();

    // The page used to carry its own sentence for SAP that asserted availability.
    expect(screen.queryByText(/Not available yet/i)).toBeNull();
    expect(screen.queryByText(/Planned: read-only access to SAP/i)).toBeNull();
  });

  it("takes availability from the server rather than deciding it", async () => {
    render(<DbConfigurationTab data={null} onRefresh={() => {}} />);

    await screen.findByText("PostgreSQL Read-only DB Link");

    // isAvailableNow true renders Available, false renders Planned. Both come from the
    // same response; nothing local chooses between them.
    expect(screen.getByText("Available")).toBeTruthy();
    expect(screen.getByText("Planned")).toBeTruthy();
  });

  it("shows nothing rather than a remembered list when the server does not answer", async () => {
    getConnectorProviderTypes.mockRejectedValue(new Error("unreachable"));

    render(<DbConfigurationTab data={null} onRefresh={() => {}} />);

    await waitFor(() => expect(getConnectorProviderTypes).toHaveBeenCalled());

    // A static fallback would quietly recreate the duplication this task removed.
    expect(screen.queryByText("PostgreSQL Read-only DB Link")).toBeNull();
    expect(screen.queryByText("SAP")).toBeNull();
  });
});