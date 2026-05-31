const fs = require("node:fs");
const path = require("node:path");

const file = path.join(
  process.cwd(),
  "Frontend",
  "PlantProcess.Web",
  "playwright.config.ts"
);

let text = fs.readFileSync(file, "utf8");

// Keep tests and spawned backend aligned.
if (!text.includes("PlantProcess__Auth__Users__0__UserName")) {
  text = text.replace(
    /ConnectionStrings__PlantProcessDb:\s*"Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123"/,
    [
      'ConnectionStrings__PlantProcessDb: "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123"',
      '        ,PlantProcess__Auth__Users__0__UserName: e2eUserName',
      '        PlantProcess__Auth__Users__0__Password: e2ePassword',
      '        PlantProcess__Auth__Users__0__Role: "Admin"',
      '        PlantProcess__Auth__Users__0__DisplayName: "E2E Admin"',
      '        PlantProcess__Auth__Users__0__Scopes__0: "plantprocess.admin"',
      '        PlantProcess__Auth__Users__0__Scopes__1: "plantprocess.configure"',
      '        PlantProcess__Auth__Users__0__Scopes__2: "plantprocess.data.manage"',
      '        PlantProcess__Auth__Users__0__Scopes__3: "plantprocess.analytics.write"',
      '        PlantProcess__Auth__Users__0__Scopes__4: "plantprocess.dashboard.write"',
      '        PlantProcess__Auth__Users__0__Scopes__5: "plantprocess.dashboard.read"',
      '        PlantProcess__Auth__SigningKey: "SuperSecretE2EOnlySigningKeyThatIsAtLeast32Bytes!!"',
      '        PlantProcess__Auth__Issuer: "PlantProcessIQ"',
      '        PlantProcess__Auth__Audience: "PlantProcessIQ.Client"',
      '        PlantProcess__Auth__AccessTokenMinutes: "60"'
    ].join(",\n        ")
  );

  // Fix accidental comma style if replacement inserted ",Plant..."
  text = text.replace(/,\s*,PlantProcess__Auth__/g, ",\n        PlantProcess__Auth__");
}

fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

console.log("Playwright backend auth env now includes e2eadmin real admin user.");
