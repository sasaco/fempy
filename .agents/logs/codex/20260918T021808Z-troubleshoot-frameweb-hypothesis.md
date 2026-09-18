The observed Ct-girder “communication error” is caused by a compressed-request wire-format mismatch, not by the Ct model, connectivity, CORS, authentication, or server availability.

| Hypothesis | Verdict | Evidence |
|---|---|---|
| 1. Transport contract/type-coercion regression | **CONFIRMED** | The frontend passes a gzip `Uint8Array` directly to `btoa`, producing Base64 of bracketless text such as `31,139,...` ([app.component.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:233)). The backend instead applies `json.loads` and requires `[31,139,...]` ([main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:153)). |
| 2. Deployment-version skew | **ELIMINATED as required for the current failure; INCONCLUSIVE historically** | The checked-in frontend and backend are inherently incompatible, so skew is unnecessary. Commit `29df328` changed permissive `eval` to `json.loads`, plausibly explaining why an older backend worked, but no deployment inventory proves that any reported environment actually used mixed versions. |
| 3a. Ct model content/validation | **ELIMINATED for the observed HTTP 400** | Failure occurs before `_read_json_model()` or `FemModel.run()` ([main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:97)). Exact Ct calculation success after repairing transport remains unproven. |
| 3b. Wrong URL/service unavailable | **ELIMINATED** | The configured service received the POST and generated a structured `invalid_input` HTTP 400. This is incompatible with refusal, timeout, or a missing service. |
| 3c. CORS | **ELIMINATED as the cause** | OPTIONS and normal responses explicitly allow the required origin, method, and headers ([main.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:68)). More decisively, the request reached backend parsing. |
| 3d. Authentication/anonymous UID | **ELIMINATED** | Authentication is only a frontend admission gate; the calculation backend does not authenticate before decompression. An empty UID reproduces the same parser error ([repro script](C:/Users/sasai/Documents/FrameWeb3/.agents/logs/repro-framewebforjs-calculation-communication-error.cjs:10)). |
| 4. Green backend tests contradict the mismatch | **ELIMINATED** | The tests construct the already-valid bracketed envelope themselves, e.g. `json.dumps(list(compressed))` ([test_axial_force_input.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/io/test_axial_force_input.py:173), [test_input_routes.py](C:/Users/sasai/Documents/FrameWeb3/FrameWeb/tests/integration/test_input_routes.py:83)). They never exercise the real JavaScript producer. |

The accepted-envelope control is causally strong:

- One identical 4,080-byte gzip stream was used for both requests.
- Current form decoded to `31,139,...` and failed JSON parsing at character 2—the comma after the valid JSON number `31`.
- Adding only JSON array brackets produced the same 4,080 bytes, ungzipped to the exact original 26,630-character JSON, and advanced the server beyond transport decoding.
- The captured run shows the current request returning the exact `Extra data` response and the accepted form reaching a later `float(None)` error ([control transcript](C:/Users/sasai/Documents/FrameWeb3/.agents/logs/codex/20260918T021808Z-troubleshoot-frameweb-flow.err.log:5462)).

That later `float(None)` is not evidence of a Ct defect: the control used an approximate raw-preset payload rather than Angular’s normalized `getInputJson(0)` output. Angular converts or removes null fixity values before calculation ([input-fix-node.service.ts](C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/input/input-fix-node/input-fix-node.service.ts:104)).

Evidence caveats:

- The accepted-envelope script logs observations but contains no assertions and exits successfully whenever both fetches complete ([accepted-envelope script](C:/Users/sasai/Documents/FrameWeb3/.agents/logs/accepted-envelope-framewebforjs-calculation-communication-error.cjs:63)).
- The nominal troubleshoot reproduction log contains a WSL launcher failure, not the product response; the successful raw run is preserved in the transcript above.
- The print API deliberately consumes bracketless CSV ([Function1.cs](C:/Users/sasai/Documents/FrameWeb3/FramePrintPDF/FramePrintAzure/Function1.cs:34)). Its superficially similar producer is a separate protocol and must not be used to infer calculation behavior.

No product files were changed.
