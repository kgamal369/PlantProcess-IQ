import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `found ${needle}`);
}

const sql = read("Backend/database/scripts/680_pack_b5_private_model_gateway_ciso_controls.sql");
const endpoint = read("Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const docs = read("docs/ai/P04_PRIVATE_MODEL_GATEWAY_CISO_CONTROLS.md");
const http = read("Documentation/v5/pack-b5-private-model-gateway-requests.http");
const lock = JSON.parse(read("Documentation/v5/model-version-lock.json"));

contains("SQL private endpoint table", sql, "ppiq_private_model_endpoint_configs");
contains("SQL policy result table", sql, "ppiq_model_gateway_policy_results");
contains("SQL governance table", sql, "ppiq_assistant_prompt_governance_events");
contains("SQL eval golden cases", sql, "ppiq_model_eval_golden_cases");
contains("SQL eval runs", sql, "ppiq_model_eval_runs");
contains("SQL private truth view", sql, "ppiq_v_private_model_gateway_truth");
contains("SQL P04 acceptance function", sql, "ppiq_v5_p04_private_model_acceptance");
contains("SQL public network false", sql, "public_network_allowed boolean NOT NULL DEFAULT false");

contains("Endpoint health route", endpoint, "/health");
contains("Endpoint validate endpoint route", endpoint, "/policies/validate-endpoint");
contains("Endpoint truth-state route", endpoint, "/policies/truth-state");
contains("Endpoint governance route", endpoint, "/governance/record");
contains("Endpoint eval route", endpoint, "/eval/run-golden");
contains("Endpoint latest eval route", endpoint, "/eval/latest");
contains("Endpoint public AI proof route", endpoint, "/policies/no-public-ai-proof");
contains("Endpoint rejects api.openai.com", endpoint, "api.openai.com");
contains("Endpoint requires ZDR", endpoint, "ZeroDataRetentionConfirmed");
contains("Endpoint hashes prompt", endpoint, "prompt_hash");
contains("Endpoint hashes response", endpoint, "response_hash");
contains("Endpoint raw not stored", endpoint, "rawPromptStored = false");
contains("Endpoint redacts secrets", endpoint, "REDACTED_SECRET");
contains("Endpoint model version pin", endpoint, "modelVersionPinned = true");

contains("Program maps private gateway endpoints", program, "MapV5PrivateModelGatewayCertificationEndpoints");

contains("Docs mention Azure private", docs, "Azure OpenAI Private Link");
contains("Docs mention Bedrock VPC", docs, "AWS Bedrock VPC endpoint");
contains("Docs mention public AI egress", docs, "public AI egress");
contains("Docs mention raw prompt/response not stored", docs, "Raw prompt/response are not stored");

contains("HTTP examples include public proof", http, "/policies/no-public-ai-proof");
contains("HTTP examples include eval", http, "/eval/run-golden");

ok("Model lock forbids public AI egress", lock.policy.publicAiEgressAllowed === false);
ok("Model lock requires ZDR", lock.policy.zeroDataRetentionRequired === true);
ok("Model lock has default model version", lock.defaultModelVersion === "local-private-mock-v1");
ok("Model lock eval rejects public endpoint", lock.eval.mustRejectPublicEndpoint === true);

console.log("");
console.log("Pack B5 private model gateway static validation passed.");