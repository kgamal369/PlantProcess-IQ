const fs = require("node:fs");

const file = "tools/phase1/apply-v7-phase01-completion.cjs";

let text = fs.readFileSync(file, "utf8");

text = text.replaceAll(
  '"${POSTGRES_PORT:-5432}:5432"',
  '"\\${POSTGRES_PORT:-5432}:5432"'
);

text = text.replaceAll(
  '"127.0.0.1:${POSTGRES_PORT:-5432}:5432"',
  '"127.0.0.1:\\${POSTGRES_PORT:-5432}:5432"'
);

fs.writeFileSync(file, text, "utf8");

console.log("OK fixed POSTGRES_PORT template escaping in V7 Phase 1 patcher.");
