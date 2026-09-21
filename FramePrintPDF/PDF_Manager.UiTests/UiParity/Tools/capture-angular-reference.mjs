import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { spawn } from "node:child_process";
import { createHash } from "node:crypto";

const chrome = process.env.CHROME_BIN ?? "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const baseUrl = process.argv[2] ?? "http://127.0.0.1:4200/";
const outputDir = resolve(process.argv[3] ?? "FramePrintPDF/PDF_Manager.UiTests/UiParity/References/angular-v1");
const debugPort = Number(process.env.FRAMEWEB_CAPTURE_PORT ?? "9337");
const profile = mkdtempSync(join(tmpdir(), "frameweb-angular-capture-"));

mkdirSync(outputDir, { recursive: true });

const browser = spawn(chrome, [
  "--headless=new",
  "--disable-gpu",
  "--no-first-run",
  "--no-default-browser-check",
  `--user-data-dir=${profile}`,
  `--remote-debugging-port=${debugPort}`,
  "--hide-scrollbars",
  "--force-device-scale-factor=1",
  "--window-size=1200,800",
  baseUrl,
], { stdio: "ignore", windowsHide: true });

let socket;
let nextCommandId = 1;
const pending = new Map();

try {
  const target = await waitForTarget();
  socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolveOpen, rejectOpen) => {
    socket.addEventListener("open", resolveOpen, { once: true });
    socket.addEventListener("error", rejectOpen, { once: true });
  });
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    const completion = pending.get(message.id);
    if (completion) {
      pending.delete(message.id);
      completion(message);
    }
  });

  await command("Page.enable");
  await command("Runtime.enable");
  await command("Emulation.setDeviceMetricsOverride", {
    width: 1200,
    height: 800,
    deviceScaleFactor: 1,
    mobile: false,
  });
  await waitForExpression("document.readyState === 'complete' && !!document.querySelector('app-root')");

  const captures = [];
  captures.push(await capture("shell-empty-1200x800-dpi100.png", "shell.empty"));

  await command("Page.navigate", { url: new URL("(startOutlet:start)", baseUrl).href });
  await waitForExpression("!!document.querySelector('app-start-menu')");
  captures.push(await capture("shell-start-overlay-1200x800-dpi100.png", "overlay.start"));
  await evaluate("document.querySelector('app-start-menu a')?.click()");
  await waitForExpression("!document.querySelector('app-start-menu')");

  for (const [elementId, fileName, state] of [
    ["3", "input-elements-1200x800-dpi100.png", "route.input-elements"],
    ["0", "input-nodes-1200x800-dpi100.png", "route.input-nodes"],
    ["1", "input-supports-1200x800-dpi100.png", "route.input-fix_nodes"],
  ]) {
    await evaluate(`document.getElementById('${elementId}')?.click()`);
    await waitForExpression("!!document.querySelector('#contents-dialog-id') && getComputedStyle(document.querySelector('#contents-dialog-id')).display !== 'none'");
    captures.push(await capture(fileName, state));
  }

  process.stdout.write(`${JSON.stringify({ ok: true, captures }, null, 2)}\n`);
} finally {
  socket?.close();
  browser.kill();
  await new Promise(resolveExit => browser.once("exit", resolveExit));
  rmSync(profile, { recursive: true, force: true });
}

async function waitForTarget() {
  const deadline = Date.now() + 15000;
  while (Date.now() < deadline) {
    try {
      const targets = await fetch(`http://127.0.0.1:${debugPort}/json`).then(response => response.json());
      const page = targets.find(target => target.type === "page");
      if (page) return page;
    } catch {
      // Chrome has not opened the DevTools endpoint yet.
    }
    await delay(100);
  }
  throw new Error("Chrome DevTools endpoint did not become ready within 15 seconds.");
}

function command(method, params = {}) {
  const id = nextCommandId++;
  return new Promise((resolveCommand, rejectCommand) => {
    pending.set(id, message => {
      if (message.error) rejectCommand(new Error(`${method}: ${message.error.message}`));
      else resolveCommand(message.result);
    });
    socket.send(JSON.stringify({ id, method, params }));
  });
}

async function evaluate(expression) {
  const result = await command("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (result.exceptionDetails) throw new Error(`Runtime.evaluate failed: ${expression}`);
  await delay(400);
  return result.result?.value;
}

async function waitForExpression(expression) {
  const deadline = Date.now() + 10000;
  while (Date.now() < deadline) {
    if (await evaluate(expression)) return;
    await delay(100);
  }
  throw new Error(`Timed out waiting for: ${expression}`);
}

async function capture(fileName, state) {
  const result = await command("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
  const path = join(outputDir, fileName);
  writeFileSync(path, Buffer.from(result.data, "base64"));
  const bytes = readFileSync(path);
  return {
    state,
    file: fileName,
    bytes: bytes.length,
    sha256: createHash("sha256").update(bytes).digest("hex").toUpperCase(),
  };
}

function delay(milliseconds) {
  return new Promise(resolveDelay => setTimeout(resolveDelay, milliseconds));
}
