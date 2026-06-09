# PlantProcess IQ - Deployment Exposure Decisions

Externally reachable surface of the PlantProcess IQ demo/staging deployment on
the Hetzner host (178.105.152.180), with rationale per binding. This is the
source the CI exposure gates (PPIQ-T208) verify.

## Public surface (intentional)

- 80:80   - HTTP, redirects to HTTPS (Caddy)
- 443:443 - HTTPS (Caddy), terminates TLS and proxies to internal services

Application services (API, workers, app-web, website) are reached only through
Caddy on 443 and are not published on host interfaces.

## Loopback-only services (not publicly reachable)

- PostgreSQL - bound to 127.0.0.1 on the host; reachable only from containers
  on the internal Docker network and from localhost. No public port.
- Jenkins - CI/CD control plane; reached via the reverse proxy with auth, not
  exposed as a raw host port for the application stack.

## External scan acceptance

We accept periodic external port scans as evidence the surface matches this
document. Reference scan:

    nmap -Pn 178.105.152.180

Expected open ports: 80 and 443 only. Any other open port is a finding to
reconcile against this document before the next deploy.

## Loopback-binding decision

Database and internal service ports use loopback (127.0.0.1) host bindings by
decision, so a misconfigured firewall cannot expose them. The reverse proxy is
the single audited ingress.
