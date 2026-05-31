const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(...parts) {
  return path.join(root, ...parts);
}

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

function writeJson(file, obj) {
  fs.writeFileSync(file, JSON.stringify(obj, null, 2) + "\n", "utf8");
  console.log("Updated " + path.relative(root, file));
}

function fixAppSettings(file) {
  if (!fs.existsSync(file)) return;

  const json = readJson(file);
  let changed = false;

  if (Object.prototype.hasOwnProperty.call(json, "Auth")) {
    json.PlantProcess ??= {};
    json.PlantProcess.Auth = {
      ...(json.Auth ?? {}),
      ...(json.PlantProcess.Auth ?? {}),
    };

    delete json.Auth;
    changed = true;
  }

  if (changed) {
    writeJson(file, json);
  } else {
    console.log("OK no root Auth section in " + path.relative(root, file));
  }
}

function fixLaunchSettings(file) {
  if (!fs.existsSync(file)) return;

  const json = readJson(file);
  let changed = false;

  const profiles = json.profiles ?? {};

  for (const profile of Object.values(profiles)) {
    const env = profile.environmentVariables;
    if (!env || typeof env !== "object") continue;

    for (const key of Object.keys(env)) {
      if (key.startsWith("Auth__")) {
        const newKey = "PlantProcess__Auth__" + key.slice("Auth__".length);
        env[newKey] ??= env[key];
        delete env[key];
        changed = true;
      }

      if (key.startsWith("Auth:")) {
        const newKey = "PlantProcess__Auth__" + key.slice("Auth:".length).replaceAll(":", "__");
        env[newKey] ??= env[key];
        delete env[key];
        changed = true;
      }
    }
  }

  if (changed) {
    writeJson(file, json);
  } else {
    console.log("OK no legacy Auth env vars in " + path.relative(root, file));
  }
}

[
  p("Backend", "PlantProcess.Api", "appsettings.json"),
  p("Backend", "PlantProcess.Api", "appsettings.Development.json"),
  p("Backend", "PlantProcess.Api", "appsettings.Local.json"),
  p("Backend", "PlantProcess.Api", "appsettings.Production.json"),
].forEach(fixAppSettings);

fixLaunchSettings(p("Backend", "PlantProcess.Api", "Properties", "launchSettings.json"));

console.log("Legacy Auth file cleanup complete.");
