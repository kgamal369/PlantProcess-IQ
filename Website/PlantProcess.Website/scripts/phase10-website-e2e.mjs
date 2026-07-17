import { spawnSync } from "node:child_process";
const npm = process.platform === "win32" ? "npm.cmd" : "npm";
const result = spawnSync(npm, ["run", "test:commercial:e2e"], { stdio: "inherit" });
process.exit(result.status ?? 1);
