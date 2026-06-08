const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function read(p) {
  return fs.readFileSync(path.join(root, p), "utf8");
}

function exists(p) {
  return fs.existsSync(path.join(root, p));
}

const required = [
  "Backend/PlantProcess.Application/Assistant/ModelGateway/NoopPrivateModelGatewayTransport.cs",
  "Backend/PlantProcess.Api/Program.cs"
];

for (const file of required) {
  if (!exists(file)) failures.push(`${file} missing`);
}

if (exists("Backend/PlantProcess.Api/Program.cs")) {
  const program = read("Backend/PlantProcess.Api/Program.cs");

  for (const signal of [
    "PPIQ_API_STARTUP_SERVICE_INFERENCE_FIX",
    "AddScoped<IPrivateModelGatewayTransport, NoopPrivateModelGatewayTransport>",
    "AddScoped<PrivateModelGatewayService>",
    "AddScoped<ConnectorBehaviourCertificationService>"
  ]) {
    if (!program.includes(signal)) failures.push(`Program.cs missing ${signal}`);
  }
}

if (exists("Backend/PlantProcess.Application/Assistant/ModelGateway/NoopPrivateModelGatewayTransport.cs")) {
  const transport = read("Backend/PlantProcess.Application/Assistant/ModelGateway/NoopPrivateModelGatewayTransport.cs");

  for (const signal of [
    "NoopPrivateModelGatewayTransport",
    "IPrivateModelGatewayTransport",
    "intentionally performs no real outbound network call"
  ]) {
    if (!transport.includes(signal)) failures.push(`NoopPrivateModelGatewayTransport.cs missing ${signal}`);
  }
}

if (failures.length) {
  console.error("PPIQ API startup service inference fix failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ API startup service inference fix passed.");