/// <reference lib="webworker" />

import { workerErrorMessage } from "../../../providers/result-worker-pipeline";

addEventListener("message", ({ data }) => {
  let error: unknown = null;
  try {
    const table = data.entries.map((entry) =>
      entry.rows.map((item) => ({
        m: item.m,
        n: item.n,
        l: item.l.toFixed(3),
        fx: item.fx.toFixed(2),
        fy: item.fy.toFixed(2),
        fz: item.fz.toFixed(2),
        mx: item.mx.toFixed(2),
        my: item.my.toFixed(2),
        mz: item.mz.toFixed(2),
        nonDummyRowIndex: item.nonDummyRowIndex,
      }))
    );
    postMessage({ table, error });
  } catch (caught) {
    error = workerErrorMessage(caught);
    postMessage({ table: [], error });
  }
});
