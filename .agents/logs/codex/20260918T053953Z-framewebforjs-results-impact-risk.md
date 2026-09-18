## Findings

1. **[Blocker] The response remains incompatible with FrameWebforJS.**  
   [main.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:113) serializes the flat solver result (`node_displacements`, `reaction_forces`, `element_stresses`). FrameWebforJS instead treats every top-level entry as a load case and requires nested `disg`, `reac`, and `fsec`; incompatible entries are silently skipped in [result-disg1.worker.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-disg/result-disg1.worker.ts:37), [result-reac1.worker.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-reac/result-reac1.worker.ts:35), and [result-fsec1.worker.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/result/result-fsec/result-fsec1.worker.ts:66). The request now succeeds with HTTP 200, but all three result views remain empty without an error. A legacy result projection is required at the compatibility boundary.

2. **[High] Legacy multi-case inputs calculate only one case.**  
   [_read_json_model](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/file_io.py:74) calls `select_case()` before analysis; [select_case](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_beam.py:6) defaults to one load case. FrameWebforJS expects the response to preserve every original case ID. Merely wrapping the current result in `{"Case1": ...}` would still silently discard cases 2–N and corrupt combinations/pickups. Compatibility requires per-case execution and projection, or an equivalent true multi-case path.

3. **[High] The new regression test locks in the incompatible result contract.**  
   [test_compressed_transport.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/io/test_compressed_transport.py:75) only checks that compressed and plain requests return the same flat response, then explicitly asserts flat fields at lines 76–79. Consequently, the suite passes while FrameWebforJS displays nothing. Add an endpoint-level contract test requiring all expected case IDs and non-empty `disg`, `reac`, and `fsec`, ideally followed by worker/UI coverage that rejects empty incompatible results.

The compressed-input parser itself appears sound and well covered. However, this change should not be described as restoring FrameWebforJS compatibility until the result-schema and multi-case findings are resolved.

Validation could not be rerun because the managed read-only environment denied access to uv’s cache. Existing repository evidence records the focused transport suite as 110 passing tests.
