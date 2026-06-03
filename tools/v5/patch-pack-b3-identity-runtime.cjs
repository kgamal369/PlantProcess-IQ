const fs = require("fs");
const path = require("path");

const root = process.cwd();
const file = path.join(root, "Backend/PlantProcess.Api/Program.cs");

let text = fs.readFileSync(file, "utf8");

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

function addMap(line) {
  if (text.includes(line)) return;

  const runIndex = text.lastIndexOf("app.Run();");
  if (runIndex >= 0) {
    text = `${text.slice(0, runIndex)}${line}\n${text.slice(runIndex)}`;
    return;
  }

  text += `\n${line}\n`;
}

addUsing("using PlantProcess.Api.EnterpriseSsoScim;");
addMap("app.MapV5IdentityRuntimeCertificationEndpoints();");

fs.writeFileSync(file, text, "utf8");
console.log("[pack-b3] Program.cs patched for identity runtime certification endpoints.");