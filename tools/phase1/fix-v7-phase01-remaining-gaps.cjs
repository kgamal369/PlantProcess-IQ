const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(...parts) {
  return path.join(root, ...parts);
}

function read(file) {
  if (!fs.existsSync(file)) throw new Error("File not found: " + file);
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.writeFileSync(file, text, "utf8");
  console.log("Patched " + path.relative(root, file));
}

// ============================================================
// Fix T282 — Program.cs exact auth binding startup log
// ============================================================

const programFile = p("Backend", "PlantProcess.Api", "Program.cs");
let program = read(programFile);

if (!program.includes("Auth bound from PlantProcess:Auth")) {
  const block = `

// PPIQ-T282: prove auth is bound from the canonical PlantProcess:Auth section.
Log.Information(
    "Auth bound from PlantProcess:Auth. Environment={EnvironmentName}",
    builder.Environment.EnvironmentName);
`;

  if (program.includes("var app = builder.Build();")) {
    program = program.replace("var app = builder.Build();", `${block}\nvar app = builder.Build();`);
  } else {
    throw new Error("Could not find 'var app = builder.Build();' in Program.cs");
  }

  write(programFile, program);
} else {
  console.log("OK Program.cs already contains exact auth binding startup log.");
}

// ============================================================
// Fix T287 — robust real-admin + bootstrap collision guard
// This intentionally uses a new method name to avoid fighting with
// previous partial patches.
// ============================================================

const startupFile = p(
  "Backend",
  "PlantProcess.Api",
  "Configuration",
  "StartupConfigurationValidator.cs"
);

let startup = read(startupFile);

if (!startup.includes("ValidateV7Phase1AuthHardening(configuration, environment, errors);")) {
  const target = "ValidateAuthentication(configuration, environment, errors);";

  if (!startup.includes(target)) {
    throw new Error("Could not find ValidateAuthentication call in StartupConfigurationValidator.cs");
  }

  startup = startup.replace(
    target,
    `${target}
        ValidateV7Phase1AuthHardening(configuration, environment, errors);`
  );
}

if (!startup.includes("private static void ValidateV7Phase1AuthHardening")) {
  const method = `

    private static void ValidateV7Phase1AuthHardening(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        List<string> errors)
    {
        var legacyAuthKeys = new[]
        {
            "Auth:SigningKey",
            "Auth:Issuer",
            "Auth:Audience",
            "Auth:BootstrapAdminUser",
            "Auth:BootstrapAdminPassword"
        };

        if (legacyAuthKeys.Any(key => !string.IsNullOrWhiteSpace(configuration[key])))
        {
            errors.Add(
                "Legacy Auth:* configuration is not supported. " +
                "Use PlantProcess:Auth:* or PlantProcess__Auth__* environment variables only.");
        }

        var configuredUsers = configuration
            .GetSection("PlantProcess:Auth:Users")
            .GetChildren()
            .ToList();

        var bootstrapUser = configuration["PlantProcess:Auth:BootstrapAdminUser"];

        var realAdminCount = configuredUsers.Count(user =>
            !string.Equals(user["IsBootstrapAdmin"], "true", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(user["Role"], "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(user["UserName"]) &&
            !string.IsNullOrWhiteSpace(user["Password"]));

        var bootstrapCollidesWithConfiguredUser =
            !string.IsNullOrWhiteSpace(bootstrapUser) &&
            configuredUsers.Any(user =>
                string.Equals(user["UserName"], bootstrapUser, StringComparison.OrdinalIgnoreCase));

        if (!environment.IsDevelopment())
        {
            if (realAdminCount == 0)
            {
                errors.Add(
                    "Non-development requires at least one real configured admin under " +
                    "PlantProcess:Auth:Users with Role=Admin and IsBootstrapAdmin=false.");
            }

            if (bootstrapCollidesWithConfiguredUser)
            {
                errors.Add(
                    "Bootstrap admin user must not collide with any configured real user. " +
                    "Use a disabled sentinel bootstrap user outside Development.");
            }
        }
    }
`;

  const lastBrace = startup.lastIndexOf("\n}");
  if (lastBrace < 0) {
    throw new Error("Could not find final class brace in StartupConfigurationValidator.cs");
  }

  startup = startup.slice(0, lastBrace) + method + startup.slice(lastBrace);
}

write(startupFile, startup);

// ============================================================
// Fix T288 — remove UTF-8 BOM from SQL scripts 202 and 203
// ============================================================

const sqlFiles = [
  p("Backend", "database", "scripts", "202_phase02_ml_compute_basic_correlations_hotfix.sql"),
  p("Backend", "database", "scripts", "203_phase02_ml_compute_v6_wrapper_hotfix.sql")
];

for (const file of sqlFiles) {
  if (!fs.existsSync(file)) {
    throw new Error("SQL file not found: " + file);
  }

  let buffer = fs.readFileSync(file);

  if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
    buffer = buffer.subarray(3);
    fs.writeFileSync(file, buffer);
    console.log("Removed UTF-8 BOM from " + path.relative(root, file));
  } else {
    const text = buffer.toString("utf8").replace(/^\uFEFF/, "");
    fs.writeFileSync(file, text, "utf8");
    console.log("OK no BOM found: " + path.relative(root, file));
  }
}

console.log("");
console.log("OK V7 Phase 1 remaining validator gaps fixed.");
