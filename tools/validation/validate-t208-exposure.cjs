const fs = require("node:fs");
const path = require("node:path");

const repo = process.cwd();
const composePath = path.join(repo, "Infrastructure", "deploy", "docker-compose.demo.yml");
const readmePath = path.join(repo, "Infrastructure", "deploy", "README.md");

function fail(message) {
  console.error("FAILED: " + message);
  process.exit(1);
}

if (!fs.existsSync(composePath)) fail("Missing docker-compose.demo.yml");
if (!fs.existsSync(readmePath)) fail("Missing Infrastructure/deploy/README.md");

const compose = fs.readFileSync(composePath, "utf8");
const readme = fs.readFileSync(readmePath, "utf8");
const unsafe = [];

for (const line of compose.split(/\r?\n/)) {
  const trimmed = line.trim();
  const match = trimmed.match(/^-\s*["']?([^"']+:[0-9]+)["']?$/);
  if (!match) continue;

  const binding = match[1];
  const isPublicHttp = binding === "80:80" || binding === "443:443";
  const isLoopback = binding.startsWith("127.0.0.1:");

  if (!isPublicHttp && !isLoopback) unsafe.push(binding);
}

if (unsafe.length > 0) fail("Unsafe public host-port bindings: " + unsafe.join(", "));

for (const token of ["PostgreSQL", "Jenkins", "External scan acceptance", "nmap -Pn 178.105.152.180"]) {
  if (!readme.includes(token)) fail("README missing exposure proof token: " + token);
}

console.log("OK PPIQ-T208 static exposure validation passed.");
