const fs = require("fs");
const path = require("path");

const root = process.cwd();
const rel = "Backend/PlantProcess.Api/Program.cs";
const full = path.join(root, rel);

let text = fs.readFileSync(full, "utf8");

if (!text.includes("using PlantProcess.Api.AssistantGateway;")) {
  const lastUsingMatch = [...text.matchAll(/^using .*?;\s*$/gm)].pop();
  if (lastUsingMatch) {
    const insertAt = lastUsingMatch.index + lastUsingMatch[0].length;
    text = text.slice(0, insertAt) + "\nusing PlantProcess.Api.AssistantGateway;" + text.slice(insertAt);
  } else {
    text = "using PlantProcess.Api.AssistantGateway;\n" + text;
  }
}

if (!text.includes("AddV5AssistantGateway(")) {
  const buildIndex = text.indexOf("var app = builder.Build();");
  const line = "builder.Services.AddV5AssistantGateway(builder.Configuration);";
  if (buildIndex >= 0) {
    text = text.slice(0, buildIndex) + line + "\n\n" + text.slice(buildIndex);
  } else {
    const firstBuilderServices = text.indexOf("builder.Services.");
    if (firstBuilderServices >= 0) {
      text = text.slice(0, firstBuilderServices) + line + "\n" + text.slice(firstBuilderServices);
    } else {
      throw new Error("Could not find a safe place to add AddV5AssistantGateway.");
    }
  }
}

if (!text.includes("MapV5AssistantGatewayEndpoints(")) {
  const runIndex = text.lastIndexOf("app.Run();");
  const line = "app.MapV5AssistantGatewayEndpoints();";
  if (runIndex >= 0) {
    text = text.slice(0, runIndex) + line + "\n\n" + text.slice(runIndex);
  } else {
    text += "\napp.MapV5AssistantGatewayEndpoints();\n";
  }
}

fs.writeFileSync(full, text, "utf8");
console.log(`[p04] patched ${rel}`);