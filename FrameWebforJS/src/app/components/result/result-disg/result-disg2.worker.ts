/// <reference lib="webworker" />

import { workerErrorMessage } from "../../../providers/result-worker-pipeline";

addEventListener("message", ({ data }) => {
  let error: unknown = null;
  try {
    const table = data.entries.map((entry) =>
      entry.rows.map((item) => ({
        id: item.id,
        dx: item.dx.toFixed(4),
        dy: item.dy.toFixed(4),
        dz: item.dz.toFixed(4),
        rx: item.rx.toFixed(4),
        ry: item.ry.toFixed(4),
        rz: item.rz.toFixed(4),
      }))
    );
    postMessage({ table, error });
  } catch (caught) {
    error = workerErrorMessage(caught);
    postMessage({ table: [], error });
  }
});
