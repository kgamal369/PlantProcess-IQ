# PlantProcess IQ — Website Premium Header Polish Evidence

Generated: 20260705-221252

## What this patch fixed

1. Replaced dirty crowded top navigation with premium industrial header.
2. Removed top-level Home/PPIQ/MES/QES/Yard/Energy/Pricing/Security/Contact crowding.
3. Logo now acts as Home.
4. Main nav now exposes Product / Solutions / Pricing / Security / Contact.
5. MES/QES/Yard/Energy are grouped under a proper Solutions flyout.
6. Header CTA is now visually premium and separated from navigation.
7. Added responsive header behavior for desktop/tablet/mobile.
8. Added hover, focus, active, card, and footer polish.
9. Added static validation script:
   - Website/PlantProcess.Website/scripts/validate-premium-header.mjs
10. Patched deploy/caddy defaults:
   - app -> plantprocess-web:80
   - website -> plantprocess-website:80
   - api -> plantprocess-api:5063
11. Best-effort compose persistence for plantprocess-website service if missing.

## Validation run

- node scripts/validate-premium-header.mjs
- node scripts/validate-website-content.mjs, if present
- node scripts/check-tagline.mjs, if present
- npm run build
- dist marker scan:
  - HMI markers absent
  - marketing markers present

## Files expected to change

- Website/PlantProcess.Website/src/App.tsx
- Website/PlantProcess.Website/src/styles/phase10.css
- Website/PlantProcess.Website/scripts/validate-premium-header.mjs
- deploy/caddy/Caddyfile, if present
- deploy/compose/docker-compose.yml, if plantprocess-website was missing
- deploy/compose/docker-compose.server.yml, if server overlay needed website edge network

## Next release step

Commit and push. Jenkins should rebuild and deploy the permanent website artifact.

Recommended commit:

git add Website/PlantProcess.Website/src/App.tsx Website/PlantProcess.Website/src/styles/phase10.css Website/PlantProcess.Website/scripts/validate-premium-header.mjs deploy/caddy/Caddyfile deploy/compose/docker-compose.yml deploy/compose/docker-compose.server.yml
git commit -m "Polish marketing website header and persist website routing"
git push origin main
