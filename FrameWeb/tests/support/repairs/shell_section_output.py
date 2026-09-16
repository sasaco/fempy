"""Explicit, numeric-value-preserving migration of the retired output schema.

No calculation or expected-value generation. Run with --write to apply.
"""

import json
import re

FIELD = "shell_fsec"


def remove_field(text):
    decoder = json.JSONDecoder()
    while match := re.search(r'"' + FIELD + r'"\s*:\s*', text):
        _, length = decoder.raw_decode(text[match.end() :])
        a, b = match.start(), match.end() + length
        while b < len(text) and text[b].isspace():
            b += 1
        if text[b] == ",":
            b += 1
        else:
            while text[a - 1].isspace():
                a -= 1
            assert text[a - 1] == ","
            a -= 1
        text = text[:a] + text[b:]
    return re.sub(r"(?m)^[ \t]+(?=\r?$)", "", text)


def without_retired(value):
    if isinstance(value, dict):
        return {k: without_retired(v) for k, v in value.items() if k != FIELD}
    if isinstance(value, list):
        return list(map(without_retired, value))
    return value
