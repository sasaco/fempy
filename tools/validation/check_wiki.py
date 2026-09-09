"""Validate Wiki links, JSON/Python fences and marked runnable examples.

Run from the repository: python -m tools.validation.check_wiki
Each example runs in a fresh temporary directory with documented fixtures.
HTTP examples use a temporary local server; no external service is contacted.
"""
from __future__ import annotations

import ast
from collections import deque
from contextlib import ExitStack
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
from threading import Thread
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[2]
WIKI = ROOT / "docs" / "wiki"
FENCE = re.compile(r"^```([^\n]*)\n(.*?)^```[ \t]*$", re.M | re.S)
MARKED = re.compile(r"<!-- (fixture|run): ([\w.-]+) -->\s*```(\w+)\n(.*?)\n```", re.S)
LINK = re.compile(r"\[[^\]]*\]\(([^)]+)\)")


def inspect_pages():
    from tools.validation.render_capabilities import check_rendered_documents

    check_rendered_documents()
    fixtures, examples, links = {}, [], {}
    json_count = python_count = 0
    names = set()
    pages = sorted(WIKI.glob("*.md"))
    for page in pages:
        source = page.read_text(encoding="utf-8")
        if source.count("\n```") % 2:
            raise ValueError(f"{page.name}: unbalanced code fences")
        links[page.resolve()] = []
        prose = FENCE.sub("", source)
        for target in LINK.findall(prose):
            parsed = urlsplit(target)
            if parsed.scheme:
                continue
            path = (page.parent / unquote(parsed.path)).resolve() if parsed.path else page.resolve()
            if not path.exists():
                raise ValueError(f"{page.name}: missing link {target}")
            if path.parent == WIKI.resolve() and path.suffix == ".md":
                links[page.resolve()].append(path)
        for language, code in FENCE.findall(source):
            if language == "python":
                ast.parse(code, filename=page.name)
                python_count += 1
            elif language == "json":
                json.loads(code, parse_constant=lambda value: (_ for _ in ()).throw(
                    ValueError(f"{page.name}: non-finite JSON {value}")))
                json_count += 1
        for kind, name, language, code in MARKED.findall(source):
            if name in names:
                raise ValueError(f"Duplicate example/fixture: {name}")
            names.add(name)
            if kind == "fixture":
                if language != "json" or Path(name).name != name:
                    raise ValueError(f"Invalid fixture: {name}")
                fixtures[name] = code
            else:
                if language != "python":
                    raise ValueError(f"Runnable example must be Python: {name}")
                examples.append((page.name, name, code))
    seen, queue = set(), deque([(WIKI / "index.md").resolve()])
    while queue:
        page = queue.popleft()
        if page not in seen:
            seen.add(page)
            queue.extend(links.get(page, []))
    unreachable = set(links) - seen
    if unreachable:
        raise ValueError(f"Pages not reachable from index: {sorted(p.name for p in unreachable)}")
    if not examples or not fixtures:
        raise ValueError("No runnable examples or input fixtures found")
    print(f"Checked {len(pages)} pages, {python_count} Python blocks, {json_count} JSON blocks and local links.")
    return fixtures, examples


def main():
    fixtures, examples = inspect_pages()
    environment = dict(os.environ, PYTHONIOENCODING="utf-8", PYTHONUTF8="1")
    environment["PYTHONPATH"] = os.pathsep.join([str(ROOT / "src"), str(ROOT)])
    with ExitStack() as cleanup:
        base_url = "http://localhost:5000/"
        if any(base_url in code for _, _, code in examples):
            from werkzeug.serving import make_server, WSGIRequestHandler
            from main import app

            class QuietHandler(WSGIRequestHandler):
                def log_request(self, *args, **kwargs):
                    pass

            server = make_server("127.0.0.1", 0, app, request_handler=QuietHandler)
            thread = Thread(target=server.serve_forever, daemon=True)
            thread.start()
            cleanup.callback(server.server_close)
            cleanup.callback(thread.join, 5)
            cleanup.callback(server.shutdown)
            local_url = f"http://127.0.0.1:{server.server_port}/"
        else:
            local_url = base_url
        for page, name, code in examples:
            with tempfile.TemporaryDirectory(prefix="FEMPython-wiki-") as work:
                for filename, content in fixtures.items():
                    (Path(work) / filename).write_text(content, encoding="utf-8")
                # Only the local server's port changes; the published code and assertions run as written.
                result = subprocess.run(
                    [sys.executable, "-c", code.replace(base_url, local_url)],
                    cwd=work, env=environment, capture_output=True, text=True,
                    encoding="utf-8", timeout=60,
                )
                if result.returncode:
                    raise RuntimeError(f"{page} / {name}\n{result.stdout[-4000:]}\n{result.stderr[-4000:]}")
                print(f"PASS {name}")
    print(f"All {len(examples)} runnable Wiki examples passed.")


if __name__ == "__main__":
    main()
