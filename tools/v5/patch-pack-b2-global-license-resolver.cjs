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

function addBeforeBuild(line) {
  if (text.includes(line)) return;

  const marker = "var app = builder.Build();";
  const index = text.indexOf(marker);

  if (index < 0) {
    throw new Error("Could not find Program.cs builder.Build marker.");
  }

  text = `${text.slice(0, index)}${line}\n${text.slice(index)}`;
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

addUsing("using PlantProcess.Api.SignedLicensing;");
addUsing("using PlantProcess.Application.Licensing.Interfaces;");

// Register this AFTER Application/Infrastructure service registration and BEFORE build.
// Microsoft DI resolves the last registration for single-service resolution.
addBeforeBuild("builder.Services.AddHttpContextAccessor();");
addBeforeBuild("builder.Services.AddScoped<ILicenseService, VerifiedEd25519LicenseService>();");

addMap("app.MapV5LicenseResolverProofEndpoints();");

fs.writeFileSync(file, text, "utf8");

console.log("[pack-b2] Program.cs patched: ILicenseService now resolves through VerifiedEd25519LicenseService.");