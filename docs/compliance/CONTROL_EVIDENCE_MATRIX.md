# PlantProcess IQ Control Evidence Matrix

| Framework | Control | Product control | Evidence |
|---|---|---|---|
| SOC 2 | CC6.1 | Sensitive action audit | public.ppiq_audit_events |
| SOC 2 | CC7.2 | Retention run evidence | public.ppiq_retention_run_logs |
| ISO 27001 | A.8.15 | Logging | /api/v5/compliance/audit/events |
| ISO 27001 | A.5.34 | Privacy / PII | /api/v5/compliance/dsar |
| GxP / ALCOA+ | Audit evidence | Hash-chained audit trail | previous_hash + event_hash |