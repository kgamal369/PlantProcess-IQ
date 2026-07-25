// ============================================================
// FILE: Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx
//
// HIGH PRIORITY ITEMS 7 + 8 + 9:
//  7. DB Link Configuration UI - Create / Edit connection profiles
//  8. Table Browser UI - Discover & register tables from a live DB
//  9. Import Job Configuration UI - Schedule import per dataset
//
// Replaces the previous read-only version.
// All API calls match the existing productApi methods exactly.
// ============================================================

import { useEffect, useState } from "react";
import {
  Boxes,
  Cloud,
  FileSpreadsheet,
  FileText,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Clock,
  Database,
  Edit2,
  Link2,
  Loader2,
  PlayCircle,
  Plus,
  RadioTower,
  RefreshCw,
  ServerCog,
  Settings2,
  TableProperties,
  Trash2,
  X,
} from "lucide-react";

import {
  productApi,
  type ConnectionProfileRecord,
  type CreateConnectionProfileRequest,
  type DbConfigurationSummary,
  type ProviderTypeRecord,
  type SourceDatasetDefinitionRecord,
  type SourceFieldDefinitionRecord,
  type UpdateConnectionImportScheduleRequest,
} from "../../api/productApiClient";
import type { DbConfigurationSourceSystem } from "../../api/product-core/admin-mapping-types";
import { ErrorPanel } from "@/components/AsyncState";
import { useOptimisticSave } from "@/hooks/useOptimisticSave";
import { AdminPanel, StatusPill, formatDate } from "./AdminSharedComponents";

import { InlineFieldError } from "@/components/forms/InlineFieldError";
import { StandardPageButton, StandardPageInput, StandardPageSelect, StandardPageTable } from "@/components/standard/StandardPageCompat";
import { P2T08_STANDARD_ROLLOUT_MARKER } from "@/components/standard/StandardP2Controls";
import {
  useInlineFormValidation,
  validateCode,
  validateIntervalMinutes,
  validatePort,
  validateRequired,
} from "@/hooks/useInlineFormValidation";
// - Local types -

interface ConnectionTestResult {
  isSuccess: boolean;
  message: string;
  testedAtUtc: string;
  metadata?: Record<string, string | null>;
}

type ViewMode = "list" | "create" | "edit" | "tables";

const PROVIDER_DEFAULTS: Record<string, { port: number; schemaName: string }> = {
  postgresql: { port: 5432, schemaName: "public" },
  sqlserver: { port: 1433, schemaName: "dbo" },
  mysql: { port: 3306, schemaName: "" },
  oracle: { port: 1521, schemaName: "" },
  csv: { port: 0, schemaName: "" },
  excel: { port: 0, schemaName: "" },
};

// - DbConfigurationTab (orchestrator) -

const PROVIDER_ICON: Record<string, React.ElementType> = {
  Csv: FileText,
  Excel: FileSpreadsheet,
  PostgreSql: Database,
  SqlServer: Database,
  MySql: Database,
  Oracle: Database,
  Sap: Boxes,
  RestApi: Cloud,
  OpcUaHistorian: RadioTower,
};

const PROVIDER_DETAIL: Record<string, string> = {
  Csv: "Point at a folder of CSV exports. Each file is read into the staging layer, then mapped to the plant model. No agent is installed on your systems.",
  Excel: "Reads Excel workbooks and named sheets into the staging layer, then maps them to the plant model. Useful for lab, QA and yard files kept in Excel.",
  PostgreSql: "Read-only DB link to a PostgreSQL database. Browses schemas and tables, imports the delta each cycle using a watermark column, and never writes to the source.",
  SqlServer: "Read-only DB link to Microsoft SQL Server. Browses schemas and tables, imports the delta each cycle using a watermark column, and never writes to the source.",
  MySql: "Read-only DB link to MySQL. Browses schemas and tables, imports the delta each cycle using a watermark column, and never writes to the source.",
  Oracle: "Read-only DB link to Oracle. Browses schemas and tables, imports the delta each cycle using a watermark column, and never writes to the source.",
  Sap: "Planned: read-only access to SAP source systems. Not available yet - SAP data can be onboarded today via file or database snapshot.",
  RestApi: "Planned: reads snapshots from REST endpoints into the staging layer.",
  OpcUaHistorian: "Planned: read-only gateway for OPC-UA historians. Browses tags and points and takes bounded sample reads for mapping.",
};
export function DbConfigurationTab({
  data,
  onRefresh,
}: {
  data: DbConfigurationSummary | null;
  onRefresh: () => Promise<void> | void;
}) {
  const [connections, setConnections] = useState<ConnectionProfileRecord[]>([]);
  const [providerTypes, setProviderTypes] = useState<ProviderTypeRecord[]>([]);
  const [viewMode, setViewMode] = useState<ViewMode>("list");
  const [editingProfile, setEditingProfile] = useState<ConnectionProfileRecord | null>(null);
  const [tableBrowserProfileId, setTableBrowserProfileId] = useState<string | null>(null);
  const [isLoadingConnections, setIsLoadingConnections] = useState(true);
  const [error, setError] = useState<unknown>(null);

  async function loadConnections() {
    setIsLoadingConnections(true);
    setError(null);
    try {
      const [profiles, types] = await Promise.all([
        productApi.getConnectionProfiles(true),
        productApi.getConnectorProviderTypes(),
      ]);
      setConnections(profiles);
      setProviderTypes(types);
    } catch (err) {
      setError(err);
    } finally {
      setIsLoadingConnections(false);
    }
  }

  useEffect(() => { loadConnections(); }, []);

  async function handleSaved() {
    await loadConnections();
    await onRefresh();
    setViewMode("list");
    setEditingProfile(null);
  }

  function openCreate() { setEditingProfile(null); setViewMode("create"); }
  function openEdit(p: ConnectionProfileRecord) { setEditingProfile(p); setViewMode("edit"); }
  function openTables(id: string) { setTableBrowserProfileId(id); setViewMode("tables"); }
  function backToList() { setViewMode("list"); setEditingProfile(null); setTableBrowserProfileId(null); }

  return (
    <section className="admin-panel-grid">

      {/* - DB Link Configuration - */}
      <AdminPanel
        title="DB Link Configuration"
        subtitle="Connection profiles to customer source databases and files"
        icon={<ServerCog size={18} />}
        wide
      >
        {error ? <ErrorPanel error={error} /> : null}

        {/* Toolbar */}
        <div className="admin-action-row">
          {viewMode !== "list" ? (
            <StandardPageButton className="secondary-button" onClick={backToList} type="button">
              <X size={14} /> Back to list
            </StandardPageButton>
          ) : (
            <StandardPageButton className="primary-button" onClick={openCreate} type="button">
              <Plus size={14} /> New Connection Profile
            </StandardPageButton>
          )}
        </div>

        {/* Views */}
        {viewMode === "list" ? (
          <ConnectionProfileList
            connections={connections}
            providerTypes={providerTypes}
            isLoading={isLoadingConnections}
            onEdit={openEdit}
            onBrowseTables={openTables}
            onRefresh={loadConnections}
          />
        ) : viewMode === "create" || viewMode === "edit" ? (
          <ConnectionProfileForm
            profile={editingProfile}
            providerTypes={providerTypes}
            sourceSystems={data?.sourceSystems ?? []}
            onSaved={handleSaved}
            onCancel={backToList}
          />
        ) : viewMode === "tables" && tableBrowserProfileId ? (
          <TableBrowser
            connectionProfileId={tableBrowserProfileId}
            connections={connections}
            onBack={backToList}
            onRefresh={onRefresh}
          />
        ) : null}
      </AdminPanel>

      {/* - Provider types grid - */}
      <AdminPanel
        title="Supported Connectors"
        subtitle="Available and planned data source provider types"
        icon={<Database size={18} />}
      >
        <div className="admin-provider-grid">
          {providerTypes.map((pt) => {
            const Icon = PROVIDER_ICON[pt.providerType] ?? Database;
            const detail = PROVIDER_DETAIL[pt.providerType] ?? pt.description;
            return (
              <div
                key={pt.providerType}
                className={`admin-provider-card ${pt.isAvailableNow ? "available" : ""}`}
                title={detail}
              >
                <div className="admin-provider-card__head">
                  <span className="admin-provider-card__icon" aria-hidden="true">
                    <Icon size={16} />
                  </span>
                  <strong>{pt.displayName ?? pt.providerType}</strong>
                  {pt.isAvailableNow ? (
                    <span className="admin-pill success">
                      <CheckCircle2 size={11} /> Available
                    </span>
                  ) : (
                    <span className="admin-pill neutral">Planned</span>
                  )}
                </div>
                <p className="admin-provider-card__desc">{pt.description}</p>
                <div className="admin-provider-caps">
                  {pt.supportsSchemaDiscovery && <span className="admin-pill info">Schema</span>}
                  {pt.supportsSnapshotImport && <span className="admin-pill info">Snapshot</span>}
                  {pt.supportsIncrementalImport && <span className="admin-pill info">Incremental</span>}
                </div>
              </div>
            );
          })}
        </div>
      </AdminPanel>

      {/* - Existing source systems (read-only overview) - */}
      {(data?.sourceSystems ?? []).length > 0 ? (
        <AdminPanel
          title="Source Systems Overview"
          subtitle="Import batch statistics per source system"
          icon={<RadioTower size={18} />}
          wide
        >
          <div className="admin-table-wrap">
            <StandardPageTable>
              <thead>
                <tr>
                  <th>Code</th><th>Name</th><th>Type</th>
                  <th>Status</th><th>Batches</th><th>Failed</th><th>Last Import</th>
                </tr>
              </thead>
              <tbody>
                {data!.sourceSystems.map((s) => (
                  <tr key={s.id}>
                    <td><strong>{s.sourceSystemCode}</strong></td>
                    <td>{s.sourceSystemName}</td>
                    <td>{s.sourceSystemType}</td>
                    <td>
                      <StatusPill
                        status={s.isActive ? "Active" : "Inactive"}
                        statusClass={s.isActive ? "success" : "neutral"}
                      />
                    </td>
                    <td>{s.importBatchCount}</td>
                    <td>{s.failedBatchCount}</td>
                    <td>{formatDate(s.lastImportAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </StandardPageTable>
          </div>
        </AdminPanel>
      ) : null}

      {/* - Import Job Scheduling - */}
      <ImportJobSchedulePanel onRefresh={onRefresh} />
    </section>
  );
}

// - ConnectionProfileList -

function ConnectionProfileList({
  connections, providerTypes, isLoading, onEdit, onBrowseTables, onRefresh,
}: {
  connections: ConnectionProfileRecord[];
  providerTypes: ProviderTypeRecord[];
  isLoading: boolean;
  onEdit: (p: ConnectionProfileRecord) => void;
  onBrowseTables: (id: string) => void;
  onRefresh: () => void;
}) {
  const [testingId, setTestingId] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, string>>({});

  async function testConnection(id: string) {
    setTestingId(id);
    try {
      const result = await productApi.testConnectionProfile(id) as unknown as ConnectionTestResult;
      setTestResults((r) => ({
        ...r,
        [id]: result.isSuccess
          ? `${result.message ?? "Connection succeeded."}`
          : `${result.message ?? "Connection failed."}`,
      }));
      await onRefresh();
    } catch (err) {
      setTestResults((r) => ({
        ...r,
        [id]: `- ${err instanceof Error ? err.message : "Test failed."}`,
      }));
    } finally {
      setTestingId(null);
    }
  }

  if (isLoading) {
    return <div className="admin-copy"><Loader2 size={16} className="spin" /> Loading connections-</div>;
  }

  if (connections.length === 0) {
    return (
      <div className="empty-insight">
        <Database size={24} />
        <strong>No connection profiles yet</strong>
        <p>Click "New Connection Profile" to configure your first data source.</p>
      </div>
    );
  }

  return (
    <div className="admin-table-wrap">
      <StandardPageTable>
        <thead>
          <tr>
            <th>Name</th><th>Provider</th><th>Host / File</th>
            <th>Database</th><th>Status</th><th>Last Test</th><th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {connections.map((conn) => (
            <>
              <tr key={conn.id}>
                <td>
                  <strong>{conn.connectionProfileName}</strong>
                </td>
                <td>
                  <span className={`admin-pill ${
                    providerTypes.find((p) => p.providerType.toLowerCase() === conn.providerType.toLowerCase())?.isAvailableNow
                      ? "success" : "neutral"}`}>
                    {conn.providerType}
                  </span>
                </td>
                <td>{conn.hostName ?? conn.fileRootPath ?? "-"}</td>
                <td>{conn.databaseName ?? "-"}</td>
                <td>
                  <StatusPill
                    status={conn.isActive ? "Active" : "Inactive"}
                    statusClass={conn.isActive ? "success" : "neutral"}
                  />
                </td>
                <td>
                  {conn.lastTestStatus ? (
                    <StatusPill
                      status={conn.lastTestStatus}
                      statusClass={conn.lastTestStatus === "Success" ? "success" : "danger"}
                    />
                  ) : "-"}
                  {conn.lastTestMessage ? (
                    <small>
                      {conn.lastTestMessage}
                    </small>
                  ) : null}
                </td>
                <td>
                  <div className="admin-action-row compact">
                    <StandardPageButton
                      className="secondary-button"
                      type="button"
                      disabled={testingId === conn.id}
                      onClick={() => testConnection(conn.id)}
                      title="Test connection"
                    >
                      {testingId === conn.id
                        ? <><Loader2 size={13} className="spin" /> Testing-</>
                        : <><Link2 size={13} /> Test</>}
                    </StandardPageButton>
                    <StandardPageButton
                      className="secondary-button"
                      type="button"
                      onClick={() => onBrowseTables(conn.id)}
                      title="Browse tables and datasets"
                    >
                      <TableProperties size={13} /> Tables
                    </StandardPageButton>
                    <StandardPageButton
                      className="secondary-button"
                      type="button"
                      onClick={() => onEdit(conn)}
                      title="Edit connection profile"
                    >
                      <Edit2 size={13} /> Edit
                    </StandardPageButton>
                  </div>
                </td>
              </tr>
              {testResults[conn.id] ? (
                <tr key={`${conn.id}-result`}>
                  <td colSpan={7}>
                    <p className="admin-test-result">{testResults[conn.id]}</p>
                  </td>
                </tr>
              ) : null}
            </>
          ))}
        </tbody>
      </StandardPageTable>
    </div>
  );
}

// - ConnectionProfileForm (Create + Edit) -

function ConnectionProfileForm({
  profile, providerTypes, sourceSystems, onSaved, onCancel,
}: {
  profile: ConnectionProfileRecord | null;
  providerTypes: ProviderTypeRecord[];
  // PPIQ-V1-18 source-system-picker: real source systems (already loaded by parent) replace the hardcoded id.
  sourceSystems: DbConfigurationSourceSystem[];
  onSaved: () => void;
  onCancel: () => void;
}) {
  const isEdit = profile !== null;

  const [form, setForm] = useState({
    connectionProfileCode: profile?.connectionProfileCode ?? "",
    connectionProfileName: profile?.connectionProfileName ?? "",
    providerType: profile?.providerType ?? "Csv",
    sourceSystemDefinitionId: "",
    hostName: profile?.hostName ?? "",
    port: profile?.port ?? 5432,
    databaseName: profile?.databaseName ?? "",
    schemaName: profile?.schemaName ?? "public",
    fileRootPath: profile?.fileRootPath ?? "",
    secretReference: profile?.secretReference ?? "",
    description: profile?.description ?? "",
    readOnlyEnforced: profile?.readOnlyEnforced ?? true,
  });

  const isFileProvider = ["csv", "excel"].includes(form.providerType.toLowerCase());
  const isDbProvider = !isFileProvider;

  type ConnectionProfileField =
  | "connectionProfileCode"
  | "connectionProfileName"
  | "providerType"
  | "hostName"
  | "port"
  | "fileRootPath"
  | "secretReference";

  const validation = useInlineFormValidation<typeof form, ConnectionProfileField>(
    form,
    (value) => ({
      connectionProfileCode: !isEdit
        ? value.connectionProfileCode.trim()
          ? validateCode(value.connectionProfileCode, "Profile code")
          : undefined
        : undefined,
      connectionProfileName: validateRequired(value.connectionProfileName, "Profile name"),
      providerType: validateRequired(value.providerType, "Provider type"),
      hostName: isDbProvider
        ? validateRequired(value.hostName, "Host / Server")
        : undefined,
      port: isDbProvider
        ? validatePort(value.port, "Port")
        : undefined,
      fileRootPath: isFileProvider
        ? validateRequired(value.fileRootPath, "File root path")
        : undefined,
      secretReference: isDbProvider
        ? validateRequired(value.secretReference, "Secret reference")
        : undefined,
    })
  );

  function handleProviderChange(provider: string) {
    const defaults = PROVIDER_DEFAULTS[provider.toLowerCase()] ?? { port: 0, schemaName: "" };
    setForm((f) => ({
      ...f,
      providerType: provider,
      port: defaults.port,
      schemaName: defaults.schemaName,
    }));
  }

  function set(field: string, value: unknown) {
    setForm((f) => ({ ...f, [field]: value }));
  }

  // - FE-HARD-005: Optimistic save -
  // Replaces ~50 lines of manual isSaving/error state + try/catch boilerplate.
  // - Button label flips to "Saving-" within ~50ms of click (no network wait).
  // - Success - green toast with auto-dismiss, then `onSaved()` is called.
  // - Failure - red toast (from apiClient) + `error` exposed below for inline display.
  // - Double-submit guard built in (button disabled + in-flight ref).
  const { isSaving, save, error } = useOptimisticSave({
    successMessage: isEdit
      ? `Connection profile "${form.connectionProfileName}" updated`
      : `Connection profile "${form.connectionProfileName}" created`,
    toastId: `save-connection-profile-${profile?.id ?? "new"}`,
    onSave: async () => {
      // Validation - throw to surface as inline error + toast.
      if (!validation.prepareSubmit()) {
        throw new Error("Please fix the highlighted fields before saving.");
      }

      if (isEdit && profile) {
        await productApi.updateConnectionProfile(profile.id, {
          connectionProfileName: form.connectionProfileName,
          connectionMode: null,
          hostName: isDbProvider ? form.hostName : null,
          port: isDbProvider ? form.port : null,
          databaseName: isDbProvider ? form.databaseName : null,
          schemaName: isDbProvider ? form.schemaName : null,
          fileRootPath: isFileProvider ? form.fileRootPath : null,
          apiBaseUrl: null,
          secretReference: form.secretReference || null,
          connectionOptionsJson: null,
          readOnlyEnforced: form.readOnlyEnforced,
          description: form.description || null,
        });
      } else {
        const request: CreateConnectionProfileRequest = {
          sourceSystemDefinitionId: form.sourceSystemDefinitionId, // PPIQ-V1-18: user-selected source system
          connectionProfileCode: form.connectionProfileCode ||
            `${form.providerType.toUpperCase()}_${Date.now()}`,
          connectionProfileName: form.connectionProfileName,
          providerType: form.providerType,
          connectionMode: null,
          hostName: isDbProvider ? form.hostName : null,
          port: isDbProvider ? form.port : null,
          databaseName: isDbProvider ? form.databaseName : null,
          schemaName: isDbProvider ? form.schemaName : null,
          fileRootPath: isFileProvider ? form.fileRootPath : null,
          apiBaseUrl: null,
          secretReference: form.secretReference || null,
          connectionOptionsJson: null,
          readOnlyEnforced: form.readOnlyEnforced,
          description: form.description || null,
          isSynthetic: false,
          sourceSystem: "PlantProcessIQ.Admin",
          sourceRecordId: null,
        };
        await productApi.createConnectionProfile(request);
      }
    },
    onSuccess: () => {
      onSaved();
    },
  });

  return (
    <div className="admin-form-card admin-form-card - wide">
      <h3>{isEdit ? `Edit: ${profile!.connectionProfileName}` : "New Connection Profile"}</h3>

      {error instanceof Error ? <div className="admin-inline-error">{error.message}</div> : null}

      <div className="admin-form-grid">

        {/* Provider type */}
        <label className="admin-form-label">
          Provider Type *
          <StandardPageSelect
            className="admin-select"
            value={
              // The catalog publishes PascalCase ("Oracle"); stored profiles use
              // lowercase ("oracle"). Without this resolution the select matches
              // no option and the browser falls back to the first one, showing an
              // Oracle profile as "CSV Snapshot". Same comparison the list view
              // above already uses.
              providerTypes.find(
                (pt) => pt.providerType.toLowerCase() === form.providerType.toLowerCase()
              )?.providerType ?? form.providerType
            }
            onChange={(e) => handleProviderChange(e.target.value)}
            disabled={isEdit}
          >
            {providerTypes.map((pt) => (
              <option key={pt.providerType} value={pt.providerType} disabled={!pt.isAvailableNow}>
                {pt.displayName ?? pt.providerType}{!pt.isAvailableNow ? " (planned)" : ""}
              </option>
            ))}
          </StandardPageSelect>
        </label>

        {/* PPIQ-V1-18 source-system-picker */}
        {!isEdit ? (
          <label className="admin-form-label">
            Source System *
            <StandardPageSelect
              className="admin-select"
              value={form.sourceSystemDefinitionId}
              onChange={(e) => set("sourceSystemDefinitionId", e.target.value)}
            >
              <option value=""> - Select a source system - </option>
              {sourceSystems.map((s) => {
                const isPlanned = s.sourceSystemCode === "SAP";
                return (
                  <option key={s.id} value={s.id} disabled={isPlanned}>
                    {s.sourceSystemName} ({s.sourceSystemCode}){isPlanned ? " \u2014 planned" : ""}
                  </option>
                );
              })}
            </StandardPageSelect>
          </label>
        ) : null}

        {/* Profile name */}
        <label
          className={`admin-form-label ${
            validation.getError("connectionProfileName") ? "invalid" : ""
          }`}
        >
          Profile Name *
          <StandardPageInput
            className="admin-input"
            value={form.connectionProfileName}
            aria-invalid={Boolean(validation.getError("connectionProfileName"))}
            aria-describedby="connection-profile-name-error"
            onBlur={() => validation.markTouched("connectionProfileName")}
            onChange={(e) => set("connectionProfileName", e.target.value)}
            placeholder="e.g. Production MES Database"
          />
          <InlineFieldError
            id="connection-profile-name-error"
            message={validation.getError("connectionProfileName")}
          />
        </label>

        {!isEdit ? (
        <label
          className={`admin-form-label ${
            validation.getError("connectionProfileCode") ? "invalid" : ""
          }`}
        >
          Profile Code
          <StandardPageInput
            className="admin-input"
            value={form.connectionProfileCode}
            aria-invalid={Boolean(validation.getError("connectionProfileCode"))}
            aria-describedby="connection-profile-code-error"
            onBlur={() => validation.markTouched("connectionProfileCode")}
            onChange={(e) => set("connectionProfileCode", e.target.value)}
            placeholder="Auto-generated if empty"
          />
          <InlineFieldError
            id="connection-profile-code-error"
            message={validation.getError("connectionProfileCode")}
          />
        </label>
        ) : null}

        {/* DB-specific fields */}
        {isDbProvider ? (
          <>
            <label
              className={`admin-form-label ${
                validation.getError("hostName") ? "invalid" : ""
              }`}
            >
              Host / Server *
              <StandardPageInput
                className="admin-input"
                value={form.hostName}
                aria-invalid={Boolean(validation.getError("hostName"))}
                aria-describedby="connection-host-error"
                onBlur={() => validation.markTouched("hostName")}
                onChange={(e) => set("hostName", e.target.value)}
                placeholder="e.g. 192.168.1.100 or db.plant.local"
              />
              <InlineFieldError
                id="connection-host-error"
                message={validation.getError("hostName")}
              />
            </label>

            <label className="admin-form-label">
              Database Name
              <StandardPageInput
                className="admin-input"
                value={form.databaseName}
                onChange={(e) => set("databaseName", e.target.value)}
                placeholder="e.g. mes_production"
              />
            </label>

            <label className="admin-form-label">
              Schema Name
              <StandardPageInput
                className="admin-input"
                value={form.schemaName}
                onChange={(e) => set("schemaName", e.target.value)}
                placeholder="e.g. public / dbo"
              />
            </label>
          </>
        ) : (
        <label
          className={`admin-form-label ${
            validation.getError("fileRootPath") ? "invalid" : ""
          }`}
        >
          File Root Path *
          <StandardPageInput
            className="admin-input"
            value={form.fileRootPath}
            aria-invalid={Boolean(validation.getError("fileRootPath"))}
            aria-describedby="connection-file-root-error"
            onBlur={() => validation.markTouched("fileRootPath")}
            onChange={(e) => set("fileRootPath", e.target.value)}
            placeholder="e.g. /data/imports or C:\\Imports"
          />
          <InlineFieldError
            id="connection-file-root-error"
            message={validation.getError("fileRootPath")}
          />
        </label>
        )}

        {/* Credentials */}
        <label
          className={`admin-form-label ${
            validation.getError("secretReference") ? "invalid" : ""
          }`}
        >
          Secret Reference
          <StandardPageInput
            className="admin-input"
            value={form.secretReference}
            aria-invalid={Boolean(validation.getError("secretReference"))}
            aria-describedby="connection-secret-error"
            onBlur={() => validation.markTouched("secretReference")}
            onChange={(e) => set("secretReference", e.target.value)}
            placeholder="Env var name e.g. PLANT_DB_USER"
          />
          <InlineFieldError
            id="connection-secret-error"
            message={validation.getError("secretReference")}
          />
          <small className="admin-form-hint">
            Username pulled from env var. Password from {"{"}SECRET_REF{"}"}_PASSWORD env var.
          </small>
        </label>

        {/* Description */}
        <label className="admin-form-label">
          Description
          <StandardPageInput
            className="admin-input"
            value={form.description}
            onChange={(e) => set("description", e.target.value)}
            placeholder="Optional note about this connection"
          />
        </label>

        {/* Read-only enforcement */}
        <label className="admin-form-label admin-form-label - checkbox">
          <StandardPageInput
            type="checkbox"
            checked={form.readOnlyEnforced}
            onChange={(e) => set("readOnlyEnforced", e.target.checked)}
          />
          Enforce read-only access (recommended)
        </label>
      </div>

      <div className="admin-form-actions">
        <StandardPageButton
          className="primary-button"
          type="button"
          onClick={save}
          disabled={isSaving}
        >
          {isSaving ? <><Loader2 size={14} className="spin" /> Saving-</> : <><PlayCircle size={14} /> {isEdit ? "Save Changes" : "Create Profile"}</>}
        </StandardPageButton>
        <StandardPageButton className="secondary-button" type="button" onClick={onCancel} disabled={isSaving}>
          Cancel
        </StandardPageButton>
      </div>
    </div>
  );
}

// - TableBrowser (Item 8) -

function TableBrowser({
  connectionProfileId, connections, onBack, onRefresh,
}: {
  connectionProfileId: string;
  connections: ConnectionProfileRecord[];
  onBack: () => void;
  onRefresh: () => Promise<void> | void;
}) {
  const profile = connections.find((c) => c.id === connectionProfileId);

  const [datasets, setDatasets] = useState<SourceDatasetDefinitionRecord[]>([]);
  const [fields, setFields] = useState<Record<string, SourceFieldDefinitionRecord[]>>({});
  const [expandedDatasetId, setExpandedDatasetId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [newDatasetForm, setNewDatasetForm] = useState({
    sourceObjectName: "",
    schemaName: profile?.schemaName ?? "",
    incrementalCursorField: "",
    refreshIntervalSeconds: 900,
    description: "",
  });
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<unknown>(null);

  async function loadDatasets() {
    setIsLoading(true);
    setError(null);
    try {
      const result = await productApi.getSourceDatasets(connectionProfileId, true);
      setDatasets(result);
    } catch (err) {
      setError(err);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => { loadDatasets(); }, [connectionProfileId]);

  async function registerDataset() {
    if (!newDatasetForm.sourceObjectName.trim()) {
      setMessage("- Table/object name is required.");
      return;
    }
    setIsCreating(true);
    setMessage(null);
    try {
      const tableName = newDatasetForm.sourceObjectName.trim();
      await productApi.createSourceDataset({
        connectionProfileId,
        datasetCode: tableName.toUpperCase().replace(/[^A-Z0-9]/g, "_"),
        datasetName: tableName,
        datasetKind: "SqlTable",
        sourceObjectName: tableName,
        sourceSchemaName: newDatasetForm.schemaName || null,
        primaryTimestampField: null,
        incrementalCursorField: newDatasetForm.incrementalCursorField || null,
        refreshIntervalSeconds: newDatasetForm.refreshIntervalSeconds,
        datasetOptionsJson: null,
        description: newDatasetForm.description || null,
        isSynthetic: false,
        sourceSystem: "PlantProcessIQ.Admin",
        sourceRecordId: null,
      });
      setMessage(`- Dataset "${tableName}" registered.`);
      setNewDatasetForm((f) => ({ ...f, sourceObjectName: "", description: "" }));
      await loadDatasets();
      await onRefresh();
    } catch (err) {
      setMessage(`- ${err instanceof Error ? err.message : "Registration failed."}`);
    } finally {
      setIsCreating(false);
    }
  }

  async function toggleFields(datasetId: string) {
    if (expandedDatasetId === datasetId) {
      setExpandedDatasetId(null);
      return;
    }
    setExpandedDatasetId(datasetId);
    if (!fields[datasetId]) {
      try {
        // For CSV: use discoverCsvSchema endpoint info
        // For others: show stored field definitions (Phase 5 will add live discovery)
        setFields((f) => ({ ...f, [datasetId]: [] }));
      } catch {
        /* ignore */
      }
    }
  }

  return (
    <div>
      <div className="admin-panel__header">
        <div>
          <h3>Table Browser - {profile?.connectionProfileName}</h3>
          <p>
            {profile?.providerType} - {profile?.hostName ?? profile?.fileRootPath ?? "No host"} - {profile?.databaseName ?? ""}
          </p>
        </div>
      </div>

      {error ? <ErrorPanel error={error} /> : null}

      {/* Register new dataset */}
      <div className="admin-form-card">
        <h3>Register Table / View as Dataset</h3>
        <p className="admin-copy">
          Enter the name of a table or view from the source database to register it as a
          dataset that can be imported and mapped.
        </p>
        <div className="admin-form-grid">
          <label className="admin-form-label">
            Table / View Name *
            <StandardPageInput
              className="admin-input"
              value={newDatasetForm.sourceObjectName}
              onChange={(e) => setNewDatasetForm((f) => ({ ...f, sourceObjectName: e.target.value }))}
              placeholder="e.g. defect_log or dbo.quality_events"
            />
          </label>
          <label className="admin-form-label">
            Schema
            <StandardPageInput
              className="admin-input"
              value={newDatasetForm.schemaName}
              onChange={(e) => setNewDatasetForm((f) => ({ ...f, schemaName: e.target.value }))}
              placeholder={profile?.schemaName ?? "public"}
            />
          </label>
          <label className="admin-form-label">
            Incremental Cursor Field
            <StandardPageInput
              className="admin-input"
              value={newDatasetForm.incrementalCursorField}
              onChange={(e) => setNewDatasetForm((f) => ({ ...f, incrementalCursorField: e.target.value }))}
              placeholder="e.g. updated_at or id"
            />
            <small className="admin-form-hint">
              Leave empty for full snapshot. Set to timestamp/ID column for delta imports.
            </small>
          </label>
          <label className="admin-form-label">
            Refresh Interval (seconds)
            <StandardPageSelect
              className="admin-select admin-select - narrow"
              value={newDatasetForm.refreshIntervalSeconds}
              onChange={(e) => setNewDatasetForm((f) => ({ ...f, refreshIntervalSeconds: Number(e.target.value) }))}
            >
              {[120, 300, 600, 900, 1800, 3600, 7200, 21600, 86400].map((v) => (
                <option key={v} value={v}>
                  {v < 60 ? `${v}s` : v < 3600 ? `${v / 60}min` : `${v / 3600}h`}
                </option>
              ))}
            </StandardPageSelect>
          </label>
          <label className="admin-form-label">
            Description
            <StandardPageInput
              className="admin-input"
              value={newDatasetForm.description}
              onChange={(e) => setNewDatasetForm((f) => ({ ...f, description: e.target.value }))}
              placeholder="Optional"
            />
          </label>
        </div>
        <div className="admin-form-actions">
          <StandardPageButton
            className="primary-button"
            type="button"
            onClick={registerDataset}
            disabled={isCreating}
          >
            {isCreating
              ? <><Loader2 size={14} className="spin" /> Registering-</>
              : <><Plus size={14} /> Register Dataset</>}
          </StandardPageButton>
        </div>
        {message ? <p className="admin-test-result">{message}</p> : null}
      </div>

      {/* Registered datasets */}
      <h3>
        Registered Datasets ({datasets.length})
      </h3>

      {isLoading ? (
        <div className="admin-copy"><Loader2 size={16} className="spin" /> Loading-</div>
      ) : datasets.length === 0 ? (
        <div className="empty-insight">
          <TableProperties size={20} />
          <strong>No datasets registered yet</strong>
          <p>Register a table above to start importing data.</p>
        </div>
      ) : (
        <div className="admin-table-wrap">
          <StandardPageTable>
            <thead>
              <tr>
                <th></th>
                <th>Dataset</th>
                <th>Kind</th>
                <th>Source Object</th>
                <th>Cursor Field</th>
                <th>Refresh</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {datasets.map((ds) => (
                <>
                  <tr key={ds.id} className={expandedDatasetId === ds.id ? "selected-row" : ""}>
                    <td>
                      <StandardPageButton
                        className="ghost-button"
                        type="button"
                        onClick={() => toggleFields(ds.id)}
                        title="Expand field details"
                      >
                        {expandedDatasetId === ds.id
                          ? <ChevronDown size={14} />
                          : <ChevronRight size={14} />}
                      </StandardPageButton>
                    </td>
                    <td>
                      <strong>{ds.datasetName}</strong>
                      <small>{ds.datasetCode}</small>
                    </td>
                    <td>{ds.datasetKind}</td>
                    <td>{ds.sourceObjectName}{ds.sourceSchemaName ? ` (${ds.sourceSchemaName})` : ""}</td>
                    <td>{ds.incrementalCursorField ?? <span>Full snapshot</span>}</td>
                    <td>
                      {ds.refreshIntervalSeconds < 3600
                        ? `${ds.refreshIntervalSeconds / 60}min`
                        : `${ds.refreshIntervalSeconds / 3600}h`}
                    </td>
                    <td>
                      <StatusPill
                        status={ds.isActive ? "Active" : "Inactive"}
                        statusClass={ds.isActive ? "success" : "neutral"}
                      />
                    </td>
                  </tr>
                  {expandedDatasetId === ds.id ? (
                    <tr key={`${ds.id}-fields`}>
                      <td colSpan={7}>
                        <div className="admin-dataset-detail">
                          {fields[ds.id]?.length > 0 ? (
                            <StandardPageTable>
                              <thead>
                                <tr><th>#</th><th>Field</th><th>Type</th><th>Nullable</th><th>PK</th><th>Timestamp</th></tr>
                              </thead>
                              <tbody>
                                {fields[ds.id].map((f) => (
                                  <tr key={f.fieldName}>
                                    <td>{f.ordinal}</td>
                                    <td><strong>{f.fieldName}</strong></td>
                                    <td>{f.sourceDataType}</td>
                                    <td>{f.isNullable ? "Yes" : "No"}</td>
                                    <td>{f.isPrimaryKeyCandidate ? "-" : ""}</td>
                                    <td>{f.isTimestampCandidate ? "-" : ""}</td>
                                  </tr>
                                ))}
                              </tbody>
                            </StandardPageTable>
                          ) : (
                            <p className="admin-copy">
                              No fields discovered for this table yet. Run discovery on the
                              connection to read its live schema, or open Prepare Import to
                              select the columns you want to bring across.
                            </p>
                          )}
                        </div>
                      </td>
                    </tr>
                  ) : null}
                </>
              ))}
            </tbody>
          </StandardPageTable>
        </div>
      )}
    </div>
  );
}

// - ImportJobSchedulePanel (Item 9) -

function ImportJobSchedulePanel({
  onRefresh,
}: {
  onRefresh: () => Promise<void> | void;
}) {
  const [connections, setConnections] = useState<ConnectionProfileRecord[]>([]);
  const [selectedConnectionId, setSelectedConnectionId] = useState("");
  const [intervalMinutes, setIntervalMinutes] = useState(15);

  useEffect(() => {
    productApi.getConnectionProfiles(true).then((result) => {
      setConnections(result);
      if (result.length > 0) setSelectedConnectionId(result[0].id);
    });
  }, []);

  // - FE-HARD-005: Optimistic save -
  // Replaces local isSaving/message state + manual try/catch.
  // Toast handles both success and failure feedback.
  // Form stays open after save - user may want to schedule another connection.
  const { isSaving, save } = useOptimisticSave({
    successMessage: `Raw snapshot schedule set to every ${intervalMinutes} minutes`,
    toastId: "save-import-schedule",
    onSave: async () => {
      if (!selectedConnectionId) {
        throw new Error("Pick a connection profile first.");
      }
      const request: UpdateConnectionImportScheduleRequest = {
        scheduleExpression: `Every ${intervalMinutes} minutes`,
        importIntervalMinutes: intervalMinutes,
      };
      await productApi.updateConnectionImportSchedule(selectedConnectionId, request);
    },
    onSuccess: async () => {
      await onRefresh();
    },
  });

  return (
    <AdminPanel
      title="Raw Snapshot Import Schedule"
      subtitle="Configure how often each DB link copies new rows into the staging layer"
      icon={<Clock size={18} />}
    >
      <p className="admin-copy">
        Each import job reads from the source database and copies rows into the PlantProcess IQ
        raw staging layer. Use the cursor field on each dataset to enable delta (incremental) imports.
      </p>

      <div className="admin-form-row">
        <label className="admin-form-label">Connection Profile</label>
        <StandardPageSelect
          className="admin-select"
          value={selectedConnectionId}
          onChange={(e) => setSelectedConnectionId(e.target.value)}
          disabled={connections.length === 0}
        >
          {connections.length === 0
            ? <option value="">No connections configured yet</option>
            : connections.map((conn) => (
              <option key={conn.id} value={conn.id}>
                {conn.connectionProfileName} ({conn.providerType})
              </option>
            ))}
        </StandardPageSelect>
      </div>

      <div className="admin-form-row">
        <label className="admin-form-label">Import Frequency</label>
        <StandardPageSelect
          className="admin-select admin-select - narrow"
          value={intervalMinutes}
          onChange={(e) => setIntervalMinutes(Number(e.target.value))}
        >
          {[2, 5, 10, 15, 30, 60, 120, 360, 720, 1440].map((v) => (
            <option key={v} value={v}>
              {v < 60 ? `Every ${v} min` : v < 1440 ? `Every ${v / 60}h` : "Once daily"}
            </option>
          ))}
        </StandardPageSelect>
      </div>

      <div className="admin-form-actions">
        <StandardPageButton
          className="primary-button"
          type="button"
          onClick={save}
          disabled={isSaving || !selectedConnectionId}
        >
          {isSaving
            ? <><Loader2 size={14} className="spin" /> Saving-</>
            : <><Settings2 size={14} /> Save Import Schedule</>}
        </StandardPageButton>
      </div>
    </AdminPanel>
  );
}

