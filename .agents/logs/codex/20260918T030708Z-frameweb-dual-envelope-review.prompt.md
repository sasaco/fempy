Perform the review NOW and return the findings in this single response. Do not ask for a plan file or another prompt. This is a concrete read-only code review, and you must not edit anything.

Repository: C:\Users\sasai\Documents\FrameWeb3. Compare current FrameWeb/main.py lines 91-160, diagnostics at FrameWeb/src/fem/diagnostics.py lines 10-87, tests under FrameWeb/tests, docs at FrameWeb/docs/wiki/endpoints.md lines 67-103, Angular producer at FrameWebforJS/src/app/app.component.ts lines 229-247, and old C:\Users\sasai\Documents\FrameWeb2\main.py lines 43-114.

Review this proposed implementation plan:
A. In FrameWeb/main.py, introduce a small private byte-envelope parser used only by Compressor.decompress.
B. Base64-decode strictly. Decode outer bytes as ASCII. First call json.loads. If and only if that raises JSONDecodeError, parse strict legacy grammar DIGITS(','DIGITS)*; no whitespace, empty tokens, signs, decimal points, exponent, brackets, or non-ASCII. If JSON parses successfully but is not a list, reject without CSV fallback.
C. For either representation require non-empty values and type(value) is int (so bool is rejected), with 0 <= value <= 255. Then bytes(values), gzip.decompress, UTF-8/JSON parse. Require inner top-level object if the existing model boundary does not already do so.
D. Convert only expected transport/input failures (bad Base64, outer ASCII/JSON/CSV/type/range, bad/truncated gzip, inner UTF-8/JSON) to InputValidationError so HTTP returns invalid_input/400. Do not catch BaseException/MemoryError or solver/internal failures.
E. Preserve Compressor.compress and the existing response asymmetry. Preserve existing canonical envelope and both observed compressed header strings: gzip and gzip,base64.
F. Add focused tests in a new FrameWeb/tests/io/test_compressed_transport.py for equivalent canonical/legacy success, the actual Angular header, strict rejects, 400 payload, and recovery; retain existing broader compressed integration/regression tests.

Evidence already established by a direct read-only Flask probe: canonical envelope plus header gzip,base64 succeeds 200; equivalent bracketless CSV fails 400 Extra data; canonical [true] currently causes 500; [256] causes 400.

Return:
1. Verdict on A-F, correcting any unsafe or incompatible detail.
2. Exact exception classes/boundaries to catch and whether base64 validate=True is compatible with the known producers.
3. A compact table of edge-case expected outcomes, covering boolean, float, string, dict, nested array, empty list, range, CSV whitespace/empty/sign/non-ASCII, malformed Base64, invalid/truncated gzip, invalid inner UTF-8/JSON, huge input, gzip and gzip,base64.
4. Concrete test names and exact pytest commands.
5. File:line evidence for every important assertion.
6. One alternative design and trade-off.
Keep result-schema compatibility explicitly out of scope.
