const fs = require("node:fs");
const path = require("node:path");

const file = path.join(
  process.cwd(),
  "Frontend",
  "PlantProcess.Web",
  "src",
  "api",
  "phase1",
  "phase1Workflow.api.ts"
);

let text = fs.readFileSync(file, "utf8");

// 1) Ensure getAccessToken import exists.
if (!/import\s+\{\s*getAccessToken\s*\}\s+from\s+["']\.\.\/http\/apiClient["'];?/.test(text)) {
  text = text.replace(
    /import\s+\{\s*API_BASE_URL\s*\}\s+from\s+["']\.\.\/apiConfig["'];?/,
    `import { API_BASE_URL } from "../apiConfig";
import { getAccessToken } from "../http/apiClient";`
  );
}

// 2) Insert buildAuthHeaders helper after imports if missing.
if (!/function\s+buildAuthHeaders\s*\(/.test(text)) {
  const helper = `

function buildAuthHeaders(headers: Record<string, string> = {}): Record<string, string> {
  const token = getAccessToken();

  if (!token) {
    return headers;
  }

  return {
    ...headers,
    Authorization: \`Bearer \${token}\`,
  };
}
`;

  text = text.replace(
    /(import\s+\{\s*getAccessToken\s*\}\s+from\s+["']\.\.\/http\/apiClient["'];?)/,
    `$1${helper}`
  );
}

// 3) Make sure old cookie mode is gone.
text = text.replace(/\s*credentials:\s*["']include["'],?/g, "");
text = text.replace(/\s*credentials:\s*["']omit["'],?/g, "");

// 4) Make sure common raw headers are using the helper.
text = text.replace(
  /headers:\s*\{\s*Accept:\s*"application\/json"\s*\}/g,
  `headers: buildAuthHeaders({ Accept: "application/json" })`
);

text = text.replace(
  /headers:\s*\{\s*Accept:\s*"application\/json",\s*"Content-Type":\s*"application\/json",\s*\}/g,
  `headers: buildAuthHeaders({
      Accept: "application/json",
      "Content-Type": "application/json",
    })`
);

fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

console.log("Fixed phase1Workflow.api.ts buildAuthHeaders compile issue.");
