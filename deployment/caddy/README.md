# PlantProcess IQ Public Demo Deployment

## DNS Records

Create these A records:

| Host | Type | Target |
|---|---|---|
| plantprocessiq.com | A | SERVER_PUBLIC_IP |
| www.plantprocessiq.com | A | SERVER_PUBLIC_IP |
| app.plantprocessiq.com | A | SERVER_PUBLIC_IP |
| api.plantprocessiq.com | A | SERVER_PUBLIC_IP |

## Caddy

Copy `Caddyfile` to the server:

```bash
sudo mkdir -p /etc/caddy
sudo cp Caddyfile /etc/caddy/Caddyfile
sudo systemctl reload caddy