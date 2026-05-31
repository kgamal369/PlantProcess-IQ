const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function write(relativePath, content) {
  const file = path.join(root, relativePath);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
  console.log("Wrote " + relativePath);
}

// ============================================================================
// 1) Fix backend PageDefinition DateTimeOffset runtime conversion.
// ============================================================================

const endpointPath = "Backend/PlantProcess.Api/Endpoints/PageBuilder/PageDefinitionEndpoints.cs";
let endpoint = read(endpointPath);

endpoint = endpoint.replace(
  /reader\.GetFieldValue<DateTimeOffset>\(9\)/g,
  "ReadDateTimeOffset(reader, 9)"
);

if (!/private static DateTimeOffset ReadDateTimeOffset/.test(endpoint)) {
  endpoint = endpoint.replace(
    /    private static PageDefinitionDto ReadDto\(NpgsqlDataReader reader\)\s*\{/,
    `    private static DateTimeOffset ReadDateTimeOffset(NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);

        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            DateTime dateTime => new DateTimeOffset(
                DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text when DateTimeOffset.TryParse(text, out var parsed) => parsed.ToUniversalTime(),
            _ => DateTimeOffset.UtcNow
        };
    }

    private static PageDefinitionDto ReadDto(NpgsqlDataReader reader)
    {`
  );
}

write(endpointPath, endpoint);

// ============================================================================
// 2) Improve E2E error output so any remaining 500 prints the API body.
// ============================================================================

const specPath = "Frontend/PlantProcess.Web/e2e/phase23-pagebuilder-persistence.spec.ts";
let spec = read(specPath);

if (!/async function responseDetails/.test(spec)) {
  spec = spec.replace(
    /function headers\(token: string\) \{[\s\S]*?\n\}/,
    `function headers(token: string) {
  return {
    Authorization: "Bearer " + token,
    Accept: "application/json",
    "Content-Type": "application/json",
  };
}

async function responseDetails(response: { status: () => number; text: () => Promise<string> }) {
  let body = "";
  try {
    body = await response.text();
  } catch {
    body = "<unable to read response body>";
  }

  return "HTTP " + response.status() + " body: " + body;
}`
  );
}

spec = spec.replace(
  /expect\(create\.ok\(\), "create failed with " \+ create\.status\(\)\)\.toBeTruthy\(\);/,
  `expect(create.ok(), "create failed with " + await responseDetails(create)).toBeTruthy();`
);

spec = spec.replace(
  /expect\(updatedResponse\.ok\(\), "update failed with " \+ updatedResponse\.status\(\)\)\.toBeTruthy\(\);/,
  `expect(updatedResponse.ok(), "update failed with " + await responseDetails(updatedResponse)).toBeTruthy();`
);

spec = spec.replace(
  /expect\(loadedResponse\.ok\(\), "load failed with " \+ loadedResponse\.status\(\)\)\.toBeTruthy\(\);/,
  `expect(loadedResponse.ok(), "load failed with " + await responseDetails(loadedResponse)).toBeTruthy();`
);

spec = spec.replace(
  /expect\(listResponse\.ok\(\), "list failed with " \+ listResponse\.status\(\)\)\.toBeTruthy\(\);/,
  `expect(listResponse.ok(), "list failed with " + await responseDetails(listResponse)).toBeTruthy();`
);

spec = spec.replace(
  /expect\(deleteResponse\.ok\(\), "delete failed with " \+ deleteResponse\.status\(\)\)\.toBeTruthy\(\);/,
  `expect(deleteResponse.ok(), "delete failed with " + await responseDetails(deleteResponse)).toBeTruthy();`
);

write(specPath, spec);

// ============================================================================
// 3) Validator.
// ============================================================================

const failures = [];

const finalEndpoint = read(endpointPath);
if (!finalEndpoint.includes("ReadDateTimeOffset(reader, 9)")) {
  failures.push("Endpoint does not use ReadDateTimeOffset(reader, 9).");
}

if (!finalEndpoint.includes("private static DateTimeOffset ReadDateTimeOffset")) {
  failures.push("Endpoint missing ReadDateTimeOffset helper.");
}

const finalSpec = read(specPath);
if (!finalSpec.includes("responseDetails")) {
  failures.push("E2E spec missing responseDetails helper.");
}

if (failures.length > 0) {
  console.error("Pack 2 Hotfix A validation failed:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("Pack 2 Hotfix A applied successfully.");
