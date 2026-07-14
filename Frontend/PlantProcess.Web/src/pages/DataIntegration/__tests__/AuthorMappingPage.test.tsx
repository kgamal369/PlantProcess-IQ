import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";

const listBatchesMock = vi.fn();
const createMock = vi.fn();
const executeMock = vi.fn();
vi.mock("@/api/integration/mappingAuthor.api", () => ({
  listImportBatches: (...a: unknown[]) => listBatchesMock(...a),
  createMappingDefinition: (...a: unknown[]) => createMock(...a),
  executeMapping: (...a: unknown[]) => executeMock(...a),
}));

import { AuthorMappingPage } from "../AuthorMappingPage";

const batch = {
  id: "b1",
  sourceSystemDefinitionId: "ssd1",
  sourceObjectName: "meltshop_defect_definitions",
  sourceSystem: "postgresql",
  status: "Completed",
  startedAtUtc: "2026-07-14T00:00:00Z",
};

describe("AuthorMappingPage (M1-04 / UI-1)", () => {
  beforeEach(() => { listBatchesMock.mockReset(); createMock.mockReset(); executeMock.mockReset(); });

  it("shows the honest empty state with no batches", async () => {
    listBatchesMock.mockResolvedValue([]);
    render(<AuthorMappingPage />);
    expect(await screen.findByText(/No import batches yet/i)).toBeTruthy();
  });

  it("blocks Save when zero field maps are complete", async () => {
    listBatchesMock.mockResolvedValue([batch]);
    render(<AuthorMappingPage />);
    await screen.findAllByText(/meltshop_defect_definitions/i);

    await userEvent.click(screen.getByRole("button", { name: /Save mapping/i }));
    expect(await screen.findByText(/Add at least one field map/i)).toBeTruthy();
    expect(createMock).not.toHaveBeenCalled();
  });

// NOTE: the const:-into-mappingJson authoring contract is proven in e2e/ui-new-surfaces
  // (real browser inputs) - jsdom remounts StandardTable cell inputs per keystroke, which is a
  // harness artifact, not a product behavior. Kept out of unit tests to avoid a flaky guess.
});