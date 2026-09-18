async function main() {
  const endpoint = process.env.FRAMEWEB_CDP_ENDPOINT || "http://127.0.0.1:9223";
  const rawExpression = process.argv.slice(2).join(" ");
  const expression = rawExpression.startsWith("base64:")
    ? Buffer.from(rawExpression.slice("base64:".length), "base64").toString("utf8")
    : rawExpression.startsWith("hex:")
      ? Buffer.from(rawExpression.slice("hex:".length), "hex").toString("utf8")
      : rawExpression;

  if (!expression) {
    throw new Error("Provide a JavaScript expression to evaluate in the FrameWeb page");
  }

  const targets = await fetch(`${endpoint}/json/list`).then((response) => response.json());
  const target = targets.find(
    (candidate) =>
      candidate.type === "page" && candidate.url.startsWith("http://127.0.0.1:4200/"),
  );
  if (!target) {
    throw new Error("FrameWeb page was not found in the Chrome DevTools target list");
  }

  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });

  let nextId = 1;
  const pending = new Map();
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    if (!message.id || !pending.has(message.id)) return;
    const { resolve, reject } = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) reject(new Error(JSON.stringify(message.error)));
    else resolve(message.result);
  });

  function send(method, params = {}) {
    const id = nextId++;
    return new Promise((resolve, reject) => {
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });
  }

  await send("Runtime.enable");
  const evaluation = await send("Runtime.evaluate", {
    expression,
    awaitPromise: true,
    returnByValue: true,
  });
  if (evaluation.exceptionDetails) {
    throw new Error(
      evaluation.exceptionDetails.exception?.description ||
        evaluation.exceptionDetails.text ||
        "Runtime.evaluate failed",
    );
  }
  console.log(JSON.stringify(evaluation.result.value));
  socket.close();
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
