"""Repository tools, invoked with python -m tools.<category>.<command>."""

import sys
from pathlib import Path

# Match the wheel's fem/app package names when using a source checkout.
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))
