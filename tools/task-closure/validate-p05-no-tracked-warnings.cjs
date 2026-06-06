const cp = require("child_process");
cp.execFileSync("node", ["tools/pack-b/validate-pack-b-p05-closure.cjs"], {
  cwd: process.cwd(),
  stdio: "inherit",
  shell: false
});
