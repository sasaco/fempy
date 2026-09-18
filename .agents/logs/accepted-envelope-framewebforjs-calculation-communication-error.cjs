const fs = require("fs");
const pako = require("../../FrameWebforJS/node_modules/pako");

const samplePath = "FrameWebforJS/src/assets/preset/サンプル（Ct桁）.json";
const data = JSON.parse(fs.readFileSync(samplePath, "utf8"));

for (const key of ["define", "combine", "pickup", "three", "result"]) {
  delete data[key];
}
data.uid = "";
data.production = false;

const json = JSON.stringify(data);
const compressed = pako.gzip(json);
const currentBody = btoa(compressed);
const acceptedBody = btoa(JSON.stringify(Array.from(compressed)));

function decodedAscii(body) {
  return Buffer.from(body, "base64").toString("ascii");
}

let currentParseError;
try {
  JSON.parse(decodedAscii(currentBody));
} catch (error) {
  currentParseError = error.message;
}

const acceptedBytes = JSON.parse(decodedAscii(acceptedBody));
const acceptedJson = pako.ungzip(new Uint8Array(acceptedBytes), { to: "string" });

console.log(JSON.stringify({
  jsonChars: json.length,
  gzipBytes: compressed.length,
  currentDecodedPrefix: decodedAscii(currentBody).slice(0, 40),
  acceptedDecodedPrefix: decodedAscii(acceptedBody).slice(0, 40),
  currentDecodedLength: decodedAscii(currentBody).length,
  acceptedDecodedLength: decodedAscii(acceptedBody).length,
  currentParseError,
  acceptedByteCount: acceptedBytes.length,
  acceptedRoundTripMatches: acceptedJson === json,
}));

async function post(name, body) {
  const response = await fetch("http://127.0.0.1:8080/", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Content-Encoding": "gzip,base64",
    },
    body,
  });
  const responseText = await response.text();
  console.log(JSON.stringify({
    name,
    status: response.status,
    ok: response.ok,
    responseChars: responseText.length,
    responsePrefix: responseText.slice(0, 200),
  }));
}

async function main() {
  await post("current", currentBody);
  await post("accepted", acceptedBody);
}

main().catch((error) => {
  console.error(error);
  process.exit(2);
});
