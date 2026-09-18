You are reviewing a read-only implementation-plan investigation in C:\Users\sasai\Documents\FrameWeb3.

Goal: plan a safe backend-first compatibility change so FrameWeb/main.py accepts BOTH:
1) current canonical compressed request envelope: Base64(JSON array of gzip byte integers), and
2) legacy FrameWebforJS envelope: Base64(bracketless comma-separated decimal gzip bytes, e.g. 31,139,...).

Constraints: no eval or literal_eval; every byte must be an exact integer in 0..255; preserve canonical clients and response wrapping; malformed compressed input must be a stable invalid_input HTTP 400; analyze only, do not edit files. Old comparison source is C:\Users\sasai\Documents\FrameWeb2\main.py. Actual Angular producer is FrameWebforJS/src/app/app.component.ts, reference only. The result-schema mismatch is a separate issue and must not be mixed into this transport fix.

Please inspect the relevant source and tests yourself, then provide:
- exact current and legacy execution flow with file:line evidence;
- recommended parser algorithm, including branch/fallback rules and strict grammar/type/range handling;
- what exact exception boundary should be converted to InputValidationError without hiding genuine internal failures;
- candidate production/test files and concrete test names/cases;
- invariants, compatibility risks, resource-exhaustion considerations, and whether Base64 validate=True is compatible with known producers;
- at least one alternative design and why it is less preferred;
- exact pytest commands for targeted and full verification.

Be skeptical: specifically test/reason about JSON booleans, floats, strings, dicts, nested arrays, empty lists, negative and >255 integers, empty/whitespace CSV tokens, leading/trailing commas, signs, non-ASCII, malformed Base64, invalid/truncated gzip, invalid inner UTF-8/JSON, extremely large input, and actual Content-Encoding values gzip and gzip,base64.
