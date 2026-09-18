const fs = require("fs");
const pako = require("../../FrameWebforJS/node_modules/pako");

const samplePath = "FrameWebforJS/src/assets/preset/サンプル（Ct桁）.json";
const data = JSON.parse(fs.readFileSync(samplePath, "utf8"));

for (const key of ["define", "combine", "pickup", "three", "result"]) {
  delete data[key];
}
data.uid = "";
data.production = false;

const body = btoa(pako.gzip(JSON.stringify(data)));

fetch("http://127.0.0.1:8080/", {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    "Content-Encoding": "gzip,base64",
  },
  body,
})
  .then(async (response) => {
    console.log(`HTTP ${response.status}`);
    console.log(await response.text());
    process.exit(response.ok ? 0 : 1);
  })
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });
