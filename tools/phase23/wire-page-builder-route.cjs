const fs = require("node:fs");

const file = "Frontend/PlantProcess.Web/src/App.tsx";
let text = fs.readFileSync(file, "utf8");

if (!text.includes("const PageBuilderPage = lazy")) {
  const lazyBlock = `
const PageBuilderPage = lazy(() =>
  import("./pages/PageBuilder/PageBuilderPage").then((m) => ({
    default: m.PageBuilderPage,
  }))
);

`;

  const marker = "const MlReadinessPage = lazy(() =>";

  if (!text.includes(marker)) {
    throw new Error("Could not find lazy-page insertion marker: const MlReadinessPage = lazy");
  }

  text = text.replace(marker, lazyBlock + marker);
}

if (!text.includes('path="/page-builder"')) {
  const marker = `                    <Route
                      path="/pages/:slug"`;

  const route = `                    <Route
                      path="/page-builder"
                      element={withPageBoundary(
                        "/page-builder",
                        "Page builder is refreshing",
                        <PageBuilderPage />
                      )}
                    />

`;

  if (!text.includes(marker)) {
    throw new Error("Could not find route insertion marker for /pages/:slug.");
  }

  text = text.replace(marker, route + marker);
}

fs.writeFileSync(file, text, "utf8");
console.log("OK: /page-builder route wired.");
