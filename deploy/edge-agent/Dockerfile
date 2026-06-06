FROM node:22-alpine

WORKDIR /app

COPY tools/edge-agent/ppiq-edge-agent.cjs /app/ppiq-edge-agent.cjs
COPY tools/edge-agent/edge-agent.sample.json /app/edge-agent.sample.json

RUN mkdir -p /app/spool && chmod +x /app/ppiq-edge-agent.cjs

ENV PPIQ_EDGE_CONFIG=/app/edge-agent.sample.json

CMD ["node", "/app/ppiq-edge-agent.cjs", "--config=/app/edge-agent.sample.json", "--dry-run", "--once"]
