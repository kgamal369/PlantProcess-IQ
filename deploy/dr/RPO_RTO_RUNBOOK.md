# PlantProcess IQ DR Runbook

## Target RPO/RTO

- Pilot / demo: RPO 24h, RTO 4h
- Production single-site: RPO 60m, RTO 4h
- Enterprise HA: RPO 15m, RTO 60m

## Backup

Run:

    .\deploy\dr\backup.ps1

## Restore drill

Run:

    .\deploy\dr\restore.ps1 -DumpFile .\backups\<stamp>\plantprocessiq.dump

## HA reference

The HA reference is documented in:

    deploy/dr/ha-reference-compose.yml

The exact production HA design must be adapted per customer environment, storage layer, and failover policy.

## Drill evidence

Record every DR drill in:

    public.ppiq_deployment_dr_drills

Required evidence:

- backup timestamp
- restore timestamp
- measured RPO
- measured RTO
- data consistency check
- failover result
- operator/reviewer