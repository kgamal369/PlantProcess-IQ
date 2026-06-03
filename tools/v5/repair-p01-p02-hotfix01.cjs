const fs = require("fs");
const path = require("path");

const root = process.cwd();

function read(rel) {
  return fs.readFileSync(path.join(root, rel), "utf8");
}

function write(rel, text) {
  fs.writeFileSync(path.join(root, rel), text, "utf8");
  console.log(`[hotfix01] wrote ${rel}`);
}

function removeBuildSigningKeys(content) {
  let next = content;

  while (next.includes("BuildSigningKeys(PlantProcess.Api.Security.AuthOptions auth)")) {
    const sig = next.indexOf("BuildSigningKeys(PlantProcess.Api.Security.AuthOptions auth)");
    const start = next.lastIndexOf("static", sig);

    if (start < 0) break;

    const braceStart = next.indexOf("{", sig);
    if (braceStart < 0) break;

    let depth = 0;
    let end = -1;

    for (let i = braceStart; i < next.length; i += 1) {
      const ch = next[i];
      if (ch === "{") depth += 1;
      if (ch === "}") {
        depth -= 1;
        if (depth === 0) {
          end = i + 1;
          break;
        }
      }
    }

    if (end < 0) break;

    // Remove trailing blank lines too.
    while (end < next.length && /\s/.test(next[end])) {
      if (next[end] === "\n" && next[end + 1] === "\n") {
        end += 1;
        break;
      }
      end += 1;
    }

    next = next.slice(0, start) + next.slice(end);
  }

  return next.replace(/\n{4,}/g, "\n\n\n");
}

function insertBuildSigningKeysAtSafeLocation(content) {
  const helper = `
static System.Collections.Generic.IEnumerable<Microsoft.IdentityModel.Tokens.SecurityKey> BuildSigningKeys(PlantProcess.Api.Security.AuthOptions auth)
{
    var keys = new System.Collections.Generic.List<Microsoft.IdentityModel.Tokens.SecurityKey>();

    if (!string.IsNullOrWhiteSpace(auth.SigningKey))
    {
        keys.Add(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(auth.SigningKey)));
    }

    foreach (var oldKey in auth.SecondarySigningKeys ?? new System.Collections.Generic.List<string>())
    {
        if (!string.IsNullOrWhiteSpace(oldKey) && oldKey.Length >= 32)
        {
            keys.Add(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(oldKey)));
        }
    }

    return keys;
}
`.trim();

  if (content.includes("BuildSigningKeys(PlantProcess.Api.Security.AuthOptions auth)")) {
    return content;
  }

  const partialProgram = content.search(/^\s*public\s+partial\s+class\s+Program\b/m);

  if (partialProgram >= 0) {
    return `${content.slice(0, partialProgram).trimEnd()}\n\n${helper}\n\n${content.slice(partialProgram)}`;
  }

  return `${content.trimEnd()}\n\n${helper}\n`;
}

function patchProgram() {
  const rel = "Backend/PlantProcess.Api/Program.cs";
  let text = read(rel);

  text = removeBuildSigningKeys(text);

  if (!text.includes("BuildSigningKeys(authMonitor.CurrentValue)")) {
    text = text.replace(
      /new\[\]\s*\{\s*new\s+SymmetricSecurityKey\(Encoding\.UTF8\.GetBytes\(authMonitor\.CurrentValue\.SigningKey\)\)\s*\}/g,
      "BuildSigningKeys(authMonitor.CurrentValue)"
    );
  }

  text = insertBuildSigningKeysAtSafeLocation(text);

  write(rel, text);
}

function patchHardeningHelper() {
  const rel = "Frontend/PlantProcess.Web/e2e/helpers/hardening.ts";
  const full = path.join(root, rel);

  if (!fs.existsSync(full)) {
    console.log(`[hotfix01] ${rel} not found; skipping`);
    return;
  }

  let text = read(rel);
  const original = text;

  // Remove direct browser storage seeding of the P01 retired access-token key.
  text = text.replace(
    /(?:window\.)?(?:localStorage|sessionStorage)\.setItem\(\s*["']plantprocess\.auth\.accessToken["'][^;]*;?/g,
    "/* P01: access token is memory-only; do not seed browser storage. */"
  );

  // Remove common page.addInitScript token seeding blocks that only existed to write token to storage.
  text = text.replace(
    /await\s+page\.addInitScript\(\s*\([^)]*token[^)]*\)\s*=>\s*\{[\s\S]*?plantprocess\.auth\.accessToken[\s\S]*?\}\s*,\s*[^)]*token[^)]*\s*\);?/g,
    "/* P01: removed addInitScript browser auth-token storage seeding. */"
  );

  text = text.replace(
    /await\s+page\.evaluate\(\s*\([^)]*token[^)]*\)\s*=>\s*\{[\s\S]*?plantprocess\.auth\.accessToken[\s\S]*?\}\s*,\s*[^)]*token[^)]*\s*\);?/g,
    "/* P01: removed page.evaluate browser auth-token storage seeding. */"
  );

  if (text !== original) {
    write(rel, text);
  } else {
    console.log(`[hotfix01] no browser-token storage pattern found in ${rel}`);
  }
}

patchProgram();
patchHardeningHelper();

console.log("[hotfix01] Program.cs and hardening helper repair complete.");