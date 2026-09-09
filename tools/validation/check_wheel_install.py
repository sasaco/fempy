"""Install one built wheel and exercise its public API in an isolated interpreter."""
from __future__ import annotations

import argparse
from pathlib import Path
import subprocess
import sys
import tempfile


SMOKE_PROGRAM = r'''
from importlib import metadata, util
from json import loads
from math import isclose
from pathlib import Path
import sys

import app
import fem
from fem import BarParameter, FemModel

expected_version, forbidden_root = sys.argv[1:]
assert metadata.version("FEMPython") == fem.__version__ == expected_version
assert not Path(fem.__file__).resolve().is_relative_to(Path(forbidden_root).resolve())
assert not Path(app.__file__).resolve().is_relative_to(Path(forbidden_root).resolve())
assert util.find_spec("main") is None

model = FemModel()
model.add_node(1, 0, 0, 0)
model.add_node(2, 1, 0, 0)
model.add_material(1, "test", E=1000, nu=0.25, density=2)
model.material.add_bar_parameter(1, BarParameter(area=2, Iy=1, Iz=1, J=1))
model.add_element(1, "bar", [1, 2], 1, section_id=1, shear_correction=False)
model.add_restraint(1, True, True, True, True, True, True)
model.add_restraint(2, False, True, True, True, True, True)
model.add_load(2, fx=100)
result = model.run("static")
assert isclose(result["node_displacements"][2]["dx"], 0.05, rel_tol=1e-12)

model.save_model("model.json")
model.save_results("results.json")
assert loads(Path("model.json").read_text(encoding="utf-8"))
saved = loads(Path("results.json").read_text(encoding="utf-8"))
assert isclose(saved["node_displacements"]["2"]["dx"], 0.05, rel_tol=1e-12)
print(f"PASS isolated wheel {fem.__version__} from {fem.__file__}")
'''


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--packages-dir", type=Path, required=True)
    parser.add_argument("--expected-version", required=True)
    args = parser.parse_args()

    wheels = sorted(args.packages_dir.glob("*.whl"))
    if len(wheels) != 1:
        raise ValueError(f"expected one wheel in {args.packages_dir}, found {len(wheels)}")
    wheel = wheels[0].resolve()
    subprocess.run(
        [sys.executable, "-m", "pip", "install", "--disable-pip-version-check", str(wheel)],
        check=True,
    )
    source_root = Path(__file__).resolve().parents[2] / "src"
    with tempfile.TemporaryDirectory(prefix="FEMPython-wheel-") as work:
        subprocess.run(
            [sys.executable, "-I", "-c", SMOKE_PROGRAM, args.expected_version, str(source_root)],
            cwd=work,
            check=True,
        )


if __name__ == "__main__":
    main()
