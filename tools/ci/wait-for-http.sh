#!/usr/bin/env sh
set -eu

URL="${1:-}"
TIMEOUT_SECONDS="${2:-60}"
EXPECTED_TEXT="${3:-}"

if [ -z "$URL" ]; then
  echo "[wait-for-http] ERROR: URL argument is required."
  exit 2
fi

echo "[wait-for-http] Waiting for: $URL"
echo "[wait-for-http] Timeout seconds: $TIMEOUT_SECONDS"

START_TS="$(date +%s)"

while true; do
  STATUS_CODE="$(curl -k -s -o /tmp/ppiq_http_body.txt -w "%{http_code}" "$URL" || true)"
  BODY="$(cat /tmp/ppiq_http_body.txt 2>/dev/null || true)"

  if [ "$STATUS_CODE" = "200" ]; then
    if [ -n "$EXPECTED_TEXT" ]; then
      echo "$BODY" | grep -E "$EXPECTED_TEXT" >/dev/null 2>&1 && {
        echo "[wait-for-http] OK: $URL returned 200 and matched expected text."
        exit 0
      }
    else
      echo "[wait-for-http] OK: $URL returned 200."
      exit 0
    fi
  fi

  NOW_TS="$(date +%s)"
  ELAPSED="$((NOW_TS - START_TS))"

  if [ "$ELAPSED" -ge "$TIMEOUT_SECONDS" ]; then
    echo "[wait-for-http] ERROR: Timeout waiting for $URL"
    echo "[wait-for-http] Last status code: $STATUS_CODE"
    echo "[wait-for-http] Last response body:"
    echo "$BODY"
    exit 1
  fi

  sleep 3
done