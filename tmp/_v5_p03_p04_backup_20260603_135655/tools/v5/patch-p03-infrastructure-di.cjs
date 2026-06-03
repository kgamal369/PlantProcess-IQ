const fs = require("fs");
const path = require("path");

const root = process.cwd();
const rel = "Backend/PlantProcess.Infrastructure/DependencyInjection.cs";
const full = path.join(root, rel);

let text = fs.readFileSync(full, "utf8");

if (!text.includes("PlantProcess.Infrastructure.TimeSeries")) {
  text = text.replace(
    /using PlantProcess\.Infrastructure\.Persistence;\r?\n/,
    "using PlantProcess.Infrastructure.Persistence;\nusing PlantProcess.Infrastructure.TimeSeries;\n"
  );
}

if (!text.includes("ITelemetryIngestionQueue")) {
  const marker = "services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));";
  if (text.includes(marker)) {
    text = text.replace(
      marker,
      `${marker}\n\n        // Doctrine v5 P03: bounded telemetry ingestion queue with backpressure.\n        services.AddSingleton<ITelemetryIngestionQueue, TelemetryIngestionQueue>();`
    );
  } else {
    text = text.replace(
      /return services;/,
      "services.AddSingleton<ITelemetryIngestionQueue, TelemetryIngestionQueue>();\n\n        return services;"
    );
  }
}

fs.writeFileSync(full, text, "utf8");
console.log(`[p03] patched ${rel}`);