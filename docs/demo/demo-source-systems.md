# PlantProcess IQ V7 demo source systems

The demo source systems are deliberately split to prove the product is a configurable manufacturing data-intelligence platform, not a single hardcoded BI schema.

## Start

```powershell
docker compose -f docker-compose.demo-sources.yml up -d
docker compose -f docker-compose.demo-sources.yml ps
```

## Sources

1. Postgres MeltShop — `meltshop-postgres`
2. Oracle Caster — `caster-oracle`
3. Oracle HSM — `hsm-oracle`
4. MSSQL PKL — `pkl-mssql`
5. Excel Yard — `excel-yard` mounted file source
6. MySQL Downtime — `downtime-mysql`
7. MySQL Parsytec — `parsytec-mysql`
8. Excel QA — `excel-qa` mounted file source

No source publishes a public host port. The app should connect by Docker service name from the internal network.
