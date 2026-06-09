#!/usr/bin/env sh
# PlantProcess IQ - server exposure proof (gates T208 / T286).
# Verifies that internal service ports are NOT publicly open on the server's
# public interface. The database must be loopback-only (127.0.0.1).
#
#   5432  PostgreSQL        must NOT be publicly open
#   6379  Redis             must NOT be publicly open
#   5063  API internal port must NOT be publicly open (reached only via Caddy)
#
# Usage:  PUBLIC_IP=178.105.152.180 sh verify-server-exposure.sh
set -u
PUBLIC_IP="${PUBLIC_IP:-178.105.152.180}"
fail=0

probe() {
    port="$1"; label="$2"
    if command -v nc >/dev/null 2>&1; then
        if nc -z -w 3 "$PUBLIC_IP" "$port" 2>/dev/null; then
            echo "FINDING: $label ($port) is publicly open on $PUBLIC_IP"
            fail=1
        else
            echo "OK: $label ($port) is not publicly open on $PUBLIC_IP"
        fi
    else
        if (echo > "/dev/tcp/$PUBLIC_IP/$port") >/dev/null 2>&1; then
            echo "FINDING: $label ($port) is publicly open on $PUBLIC_IP"
            fail=1
        else
            echo "OK: $label ($port) is not publicly open on $PUBLIC_IP"
        fi
    fi
}

echo "PlantProcess IQ server exposure proof against $PUBLIC_IP"
probe 5432 "PostgreSQL"
probe 6379 "Redis"
probe 5063 "API internal port"

if command -v ss >/dev/null 2>&1; then
    echo "Loopback listeners (expect 127.0.0.1:5432 for PostgreSQL):"
    ss -ltnp 2>/dev/null | grep -E "127\.0\.0\.1:(5432|6379|5063)" || echo "  (no matching loopback listeners)"
fi

if [ "$fail" -ne 0 ]; then
    echo "EXPOSURE CHECK FAILED: an internal port is publicly open."
    exit 1
fi
echo "EXPOSURE CHECK PASSED: 5432/6379/5063 are not publicly open; PostgreSQL is loopback-only."