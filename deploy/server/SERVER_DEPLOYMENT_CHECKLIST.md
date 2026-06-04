# PlantProcess IQ - Server Deployment Checklist

## Before deployment

- [ ] Pack 2D final env/profile standardization is green.
- [ ] Pack 3A deployment baseline is green.
- [ ] deploy/server/.env.example exists and is tracked as a safe template.
- [ ] deploy/server/.env.production is created on the server only and is not tracked.
- [ ] Real DB password is not in appsettings files.
- [ ] Real JWT signing key is not in appsettings files.
- [ ] Caddyfile exists under deploy/caddy.
- [ ] Demo source compose file exists under deploy/demo-sources.

## During deployment

- [ ] Server env values are copied from templates and filled on the server.
- [ ] Docker command is available.
- [ ] Main DB starts or managed DB is reachable.
- [ ] API starts.
- [ ] Frontend starts or static build is served.
- [ ] Website starts or static build is served.
- [ ] Reverse proxy routes API/frontend/website.

## After deployment

- [ ] /health is reachable.
- [ ] /readiness is reachable.
- [ ] Login works with configured smoke user.
- [ ] Browser console has no broken API base URL.
- [ ] Demo data is available if demo mode is enabled.
- [ ] No runtime env file is committed back to Git.
