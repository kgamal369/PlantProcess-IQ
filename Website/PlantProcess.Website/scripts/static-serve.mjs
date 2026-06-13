/* Deterministic static server for the built dist/ (SPA fallback). Used by the
 * go-live gates instead of `vite preview` so the lifecycle is fully controlled. */
import { createServer } from "node:http";
import { readFile, stat } from "node:fs/promises";
import { join, extname, normalize } from "node:path";
const PORT = Number(process.argv[2] || 4173);
const ROOT = join(process.cwd(), "dist");
const TYPES = { ".html": "text/html", ".js": "text/javascript", ".mjs": "text/javascript", ".css": "text/css", ".json": "application/json", ".svg": "image/svg+xml", ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".ico": "image/x-icon", ".webp": "image/webp", ".woff2": "font/woff2", ".woff": "font/woff", ".ttf": "font/ttf", ".map": "application/json" };
const server = createServer(async (req, res) => {
  try {
    const p = decodeURIComponent((req.url || "/").split("?")[0]);
    let file = normalize(join(ROOT, p));
    if (!file.startsWith(ROOT)) { res.writeHead(403); return res.end(); }
    let s = await stat(file).catch(() => null);
    if (s && s.isDirectory()) { file = join(file, "index.html"); s = await stat(file).catch(() => null); }
    if (!s) {
      if (!extname(p)) { file = join(ROOT, "index.html"); }   // SPA fallback
      else { res.writeHead(404); return res.end("not found"); }
    }
    const body = await readFile(file);
    res.writeHead(200, { "content-type": TYPES[extname(file)] || "application/octet-stream" });
    res.end(body);
  } catch (e) { res.writeHead(500); res.end(String(e)); }
});
server.listen(PORT, "127.0.0.1", () => console.log(`static serve dist on http://127.0.0.1:${PORT}`));