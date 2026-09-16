"""Build FrameWeb.sln first; exercise startup, HTTP services and forced cleanup.

Run: FrameWeb/.venv/Scripts/python.exe scripts/smoke-local.py
Requires Windows and the prerequisites listed in README.md.
"""
import base64
import gzip
import json
from pathlib import Path
import socket
import subprocess
import time
from urllib.error import URLError
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "tools/FrameWeb.Startup/bin/Debug/net8.0/FrameWeb.Startup.dll"
PORTS = (4200, 7071, 8080)


def listening(port):
    with socket.socket() as sock:
        sock.settimeout(0.2)
        return sock.connect_ex(("127.0.0.1", port)) == 0


def request(url, data=None, headers=None, method=None, timeout=30):
    with urlopen(Request(url, data=data, headers=headers or {}, method=method), timeout=timeout) as response:
        return response.read(), response.headers


def wait_for_state(process, expected, timeout):
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        assert process.poll() is None, "Launcher exited; see .local/smoke-local.log"
        try:
            data, _ = request("http://127.0.0.1:7071/health/ready")
            state = json.loads(data)
            if state != last:
                print(state, flush=True)
                last = state
            if state["status"] == expected:
                return state
            assert state["status"] != "failed", state
        except (URLError, TimeoutError, ConnectionError):
            pass
        time.sleep(1)
    raise AssertionError(f"Startup timed out: {last}")


def stop(process):
    if process.poll() is None:
        # VS Stop Debugging may terminate the host without running StopAsync.
        process.kill()
        process.wait(timeout=15)


def wait_for_closed(ports):
    deadline = time.monotonic() + 15
    while time.monotonic() < deadline:
        if not any(listening(port) for port in ports):
            return
        time.sleep(0.2)
    raise AssertionError(f"A child server was left running: {[p for p in ports if listening(p)]}")


def main():
    assert DLL.exists(), "Run dotnet build FrameWeb.sln first"
    assert not any(listening(p) for p in PORTS), "Stop existing local servers before the smoke check"
    (ROOT / ".local").mkdir(exist_ok=True)
    with (ROOT / ".local/smoke-local.log").open("w", encoding="utf-8") as log:
        process = subprocess.Popen(["dotnet", str(DLL)], cwd=ROOT, stdout=log, stderr=log)
        try:
            state = wait_for_state(process, "ready", 1200)
            assert state["engine"] and state["frontend"]
            data, _ = request("http://127.0.0.1:8080/")
            assert json.loads(data)["results"] == "Hello World!"
            html, _ = request("http://127.0.0.1:4200/", timeout=120)
            assert b"<app-root" in html
            model = {
                "ver": "2.5.12", "dimension": 3, "language": "ja", "hasPrintInputData": True,
                "node": {"1": {"x": 0, "y": 0, "z": 0}, "2": {"x": 1, "y": 0, "z": 0}},
                "member": {"1": {"ni": 1, "nj": 2, "e": 1, "cg": 0}},
                "element": {"1": {"1": {"E": 1000, "G": 400, "A": 2, "Iy": 1, "Iz": 1, "J": 1}}},
                "fix_node": {"1": [dict(n=1, tx=1, ty=1, tz=1, rx=1, ry=1, rz=1)]},
                "load": {"1": {"name": "Axial", "symbol": "L1", "element": 1, "fix_node": 1,
                                 "load_node": [dict(n=2, tx=100)]}}
            }
            raw = json.dumps(model).encode()
            result, _ = request("http://127.0.0.1:8080/", raw, {"Content-Type": "application/json"})
            assert abs(json.loads(result)["node_displacements"]["2"]["dx"] - 0.05) < 1e-10
            compressed = gzip.compress(raw)
            body = base64.b64encode(",".join(map(str, compressed)).encode())
            pdf, _ = request("http://127.0.0.1:7071/api/Function1", body, {"Content-Type": "application/json"})
            decoded = base64.b64decode(pdf)
            assert decoded.startswith(b"%PDF-") and len(decoded) > 1000
            (ROOT / ".local/smoke-local.pdf").write_bytes(decoded)
            _, headers = request("http://127.0.0.1:7071/api/Function1", headers={
                "Origin": "http://127.0.0.1:4200", "Access-Control-Request-Method": "POST",
                "Access-Control-Request-Headers": "content-type,responsetype"
            }, method="OPTIONS")
            assert headers["Access-Control-Allow-Origin"] == "http://127.0.0.1:4200"
            print("PASS: frontend, real engine calculation, PDF and CORS", flush=True)
        finally:
            stop(process)
            wait_for_closed(PORTS)
        print("PASS: forcibly stopping the host terminated both child servers", flush=True)

        with socket.socket() as occupied:
            occupied.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            occupied.bind(("127.0.0.1", 8080))
            occupied.listen()
            process = subprocess.Popen(["dotnet", str(DLL)], cwd=ROOT, stdout=log, stderr=log)
            try:
                state = wait_for_state(process, "failed", 30)
                assert "8080" in state["message"]
                assert not listening(4200)
                assert listening(8080), "The existing server must not be stopped"
            finally:
                stop(process)
                wait_for_closed((4200, 7071))
        print("PASS: occupied-port failure preserved the existing server", flush=True)


if __name__ == "__main__":
    main()
