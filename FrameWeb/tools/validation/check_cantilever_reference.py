import argparse
import json

from tests.support.oracles.cantilever_decimal import SOURCE, reference_values


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Explicitly replace beam001 result only")
    parser.add_argument("--check", action="store_true", help="Check stored values without writing (default)")
    args = parser.parse_args()
    assert not (args.write and args.check)
    raw = SOURCE.read_bytes()
    data = json.loads(raw)
    expected = reference_values(data)
    if args.write:
        # Preserve the entire user-authored analysis input byte for byte.
        marker = b'    "result": '
        prefix, _, tail = raw.partition(marker)
        assert tail
        tail_text = tail.decode("utf-8")
        old_result, end = json.JSONDecoder().raw_decode(tail_text)
        assert old_result == data["result"]
        suffix = tail_text[end:].encode("utf-8")
        newline = b"\r\n" if b"\r\n" in raw else b"\n"
        rendered = json.dumps(expected, ensure_ascii=False, indent=4, allow_nan=False)
        rendered = rendered.replace("\n", "\n    ").encode("utf-8").replace(b"\n", newline)
        updated = prefix + marker + rendered + suffix
        assert {k: v for k, v in json.loads(updated).items() if k != "result"} == {
            k: v for k, v in data.items() if k != "result"
        }
        assert SOURCE.read_bytes() == raw, "Input changed during generation"
        SOURCE.write_bytes(updated)
    else:
        assert data["result"] == expected, "Stored result differs from the independent decimal reference"
    print(f"{len(expected)} snapshots (0..{len(expected) - 1}): " + ("written" if args.write else "verified"))


if __name__ == "__main__":
    main()
