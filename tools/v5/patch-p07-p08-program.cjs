const fs = require("fs");
const path = require("path");

const root = process.cwd();
const rel = "Backend/PlantProcess.Api/Program.cs";
const full = path.join(root, rel);

let text = fs.readFileSync(full, "utf8");

function addUsing(line) {
  if (text.includes(line)) return;

  const matches = [...text.matchAll(/^using .*?;\s*$/gm)];
  if (matches.length === 0) {
    text = `${line}\n${text}`;
    return;
  }

  const last = matches[matches.length - 1];
  const insertAt = last.index + last[0].length;
  text = `${text.slice(0, insertAt)}\n${line}${text.slice(insertAt)}`;
}

addUsing("using PlantProcess.Api.PlantConnectors;");
addUsing("using PlantProcess.Api.EnterpriseIdentity;");

function addMap(line) {
  if (text.includes(line)) return;

  const runIndex = text.lastIndexOf("app.Run();");
  if (runIndex >= 0) {
    text = `${text.slice(0, runIndex)}${line}\n${text.slice(runIndex)}`;
    return;
  }

  text += `\n${line}\n`;
}

addMap("app.MapV5PlantConnectorEndpoints();");
addMap("app.MapV5EnterpriseIdentityEndpoints();");

fs.writeFileSync(full, text, "utf8");
console.log(`[p07-p08] patched ${rel}`);