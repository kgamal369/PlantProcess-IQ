import { describe, expect, it } from "vitest";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { basename, extname, join, relative } from "node:path";

function projectPath(relativePath: string) {
  return join(process.cwd(), relativePath);
}

function walk(dir: string, output: string[] = []) {
  if (!existsSync(dir)) {
    return output;
  }

  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite", "storybook-static", "__snapshots__"].includes(entry.name)) {
        continue;
      }

      walk(full, output);
      continue;
    }

    if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function countLines(text: string) {
  return text.split(/\r?\n/).length;
}

describe("C3/C4 large file architecture boundaries", () => {
  it("implementation facades should stay small", () => {
    const files = walk(projectPath("src"));

    const facades = files.filter((file) => {
      const text = readFileSync(file, "utf8");
      return text.includes(".implementation") && !file.includes(".implementation.");
    });

    expect(facades.length, "expected at least one C3/C4 facade").toBeGreaterThan(0);

    for (const file of facades) {
      const relativeFile = relative(process.cwd(), file).replaceAll("\\", "/");
      const lines = countLines(readFileSync(file, "utf8"));

      expect(lines, `${relativeFile} must stay a small compatibility facade`).toBeLessThanOrEqual(40);
    }
  });

  it("implementation files should not be imported directly by unrelated modules", () => {
    const files = walk(projectPath("src"));
    const offenders: string[] = [];

    for (const file of files) {
      const text = readFileSync(file, "utf8");
      const relativeFile = relative(process.cwd(), file).replaceAll("\\", "/");
      const baseName = basename(relativeFile, extname(relativeFile));

      const matches = [
        ...text.matchAll(/from\s+["']([^"']*\.implementation)["']/g),
        ...text.matchAll(/import\s*\(\s*["']([^"']*\.implementation)["']\s*\)/g)
      ];

      for (const match of matches) {
        const importPath = match[1];
        const isOwnFacadeImport = importPath === `./${baseName}.implementation`;

        if (!isOwnFacadeImport) {
          offenders.push(`${relativeFile} -> ${importPath}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("legacy PlantProcess API name must not return", () => {
    const files = walk(projectPath("src"));
    const offenders: string[] = [];

    for (const file of files) {
      const text = readFileSync(file, "utf8");
      const relativeFile = relative(process.cwd(), file).replaceAll("\\", "/");

      if (text.includes("plantProcessApi")) {
        offenders.push(relativeFile);
      }
    }

    expect(offenders).toEqual([]);
  });
});