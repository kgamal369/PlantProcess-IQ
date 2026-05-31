const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const file = path.join(
  root,
  "Frontend",
  "PlantProcess.Web",
  "src",
  "api",
  "phase1",
  "phase1Workflow.api.ts"
);

if (!fs.existsSync(file)) {
  throw new Error("Missing file: " + file);
}

let text = fs.readFileSync(file, "utf8");

// Remove old/duplicate imports and any broken helper body.
text = text.replace(/^import\s+\{\s*API_BASE_URL\s*\}\s+from\s+["']\.\.\/apiConfig["'];?\s*$/gm, "");
text = text.replace(/^import\s+\{\s*getAccessToken\s*\}\s+from\s+["']\.\.\/http\/apiClient["'];?\s*$/gm, "");

// Remove a previous helper if it exists.
text = text.replace(
  /function\s+buildAuthHeaders\s*\(\s*headers:\s*Record<string,\s*string>\s*=\s*\{\}\s*\):\s*Record<string,\s*string>\s*\{[\s\S]*?\n\}/m,
  ""
);

// Remove accidental cookie credentials mode.
text = text.replace(/\s*credentials\s*:\s*["']include["'],?/g, "");
text = text.replace(/\s*credentials\s*:\s*["']omit["'],?/g, "");

// Replace raw JSON headers with bearer-token headers.
text = text.replace(
  /headers\s*:\s*\{\s*Accept\s*:\s*["']application\/json["']\s*\}/g,
  'headers: buildAuthHeaders({ Accept: "application/json" })'
);

text = text.replace(
  /headers\s*:\s*\{\s*Accept\s*:\s*["']application\/json["']\s*,\s*["']Content-Type["']\s*:\s*["']application\/json["']\s*,?\s*\}/g,
  [
    'headers: buildAuthHeaders({',
    '      Accept: "application/json",',
    '      "Content-Type": "application/json",',
    '    })'
  ].join("\n")
);

const header = [
  'import { API_BASE_URL } from "../apiConfig";',
  'import { getAccessToken } from "../http/apiClient";',
  '',
  'function buildAuthHeaders(headers: Record<string, string> = {}): Record<string, string> {',
  '  const token = getAccessToken();',
  '',
  '  if (!token) {',
  '    return headers;',
  '  }',
  '',
  '  return {',
  '    ...headers,',
  '    Authorization: `Bearer ${token}`,',
  '  };',
  '}',
  ''
].join("\n");

text = header + "\n" + text.trimStart();

fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

const finalText = fs.readFileSync(file, "utf8");

const helperCount = (finalText.match(/function\s+buildAuthHeaders\s*\(/g) ?? []).length;
if (helperCount !== 1) {
  throw new Error("Expected exactly one buildAuthHeaders helper, found " + helperCount);
}

if (!finalText.includes("getAccessToken")) {
  throw new Error("getAccessToken import/use was not applied.");
}

if (!finalText.includes("Authorization: `Bearer ${token}`")) {
  throw new Error("Authorization bearer header was not applied.");
}

console.log("Repaired phase1Workflow.api.ts bearer-token helper successfully.");
