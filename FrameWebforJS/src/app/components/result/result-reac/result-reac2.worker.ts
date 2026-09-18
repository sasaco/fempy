/// <reference lib="webworker" />

import { workerErrorMessage } from "../../../providers/result-worker-pipeline";

addEventListener("message", ({ data }) => {
  let error: unknown = null;
  try {
    const table = data.entries.map((entry) =>
      entry.rows.map((item) => ({
        id: item.id,
        tx: item.tx.toFixed(2),
        ty: item.ty.toFixed(2),
        tz: item.tz.toFixed(2),
        mx: item.mx.toFixed(2),
        my: item.my.toFixed(2),
        mz: item.mz.toFixed(2),
      }))
    );
    postMessage({ table, error });
  } catch (caught) {
    error = workerErrorMessage(caught);
    postMessage({ table: [], error });
  }
});
