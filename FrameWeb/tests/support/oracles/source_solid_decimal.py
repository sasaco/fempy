"""50-digit evaluation of the ORIGINAL original source shape-function expressions.

The expressions are read from SolidElement.js, not the Python element. Only
literal arrays and arithmetic are interpreted (no JavaScript execution/eval).
This oracle retains stiffness rounding tails through global assembly.
"""

import ast
import hashlib
import re
from decimal import Decimal as D
from decimal import localcontext
from functools import lru_cache
from itertools import product
from pathlib import Path

from tests.support.paths import ROOT
from tests.support.provenance import read_source_records


def evaluate(node, env):
    if isinstance(node, ast.Constant):
        return D(str(node.value))
    if isinstance(node, ast.Name):
        return env[node.id]
    if isinstance(node, ast.List):
        return [evaluate(n, env) for n in node.elts]
    if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.USub):
        return -evaluate(node.operand, env)
    if isinstance(node, ast.BinOp):
        a, b = evaluate(node.left, env), evaluate(node.right, env)
        if isinstance(node.op, ast.Add):
            return a + b
        if isinstance(node.op, ast.Sub):
            return a - b
        if isinstance(node.op, ast.Mult):
            return a * b
        if isinstance(node.op, ast.Div):
            return a / b
    raise ValueError("Unsupported source expression")


@lru_cache(None)
def expressions(kind):
    source = (ROOT / "docs/v0/src/SolidElement.js").read_text(encoding="utf8")
    body = re.search(
        re.escape(kind) + r"\.prototype\.shapeFunction=function\(xsi,eta,zeta\)\{(.*?)\n\};", source, re.S
    )[1]
    variables = [
        (name, ast.parse(value, mode="eval").body)
        for name, value in re.findall(r"var (\w+)=(.*?);", body, re.S)
    ]
    result = ast.parse(re.search(r"return (.*);", body, re.S)[1].strip(), mode="eval").body
    return variables, result


def quadrature(kind):
    if kind == "TetraElement2":
        a, b = (5 - D(5).sqrt()) / 20, (5 + 3 * D(5).sqrt()) / 20
        return [(p, D(1) / 24) for p in [(a, a, a), (b, a, a), (a, b, a), (a, a, b)]]
    x = [-(D(3) / 5).sqrt(), D(0), (D(3) / 5).sqrt()]
    w = [D(5) / 9, D(8) / 9, D(5) / 9]
    if kind == "WedgeElement2":
        return [
            ((r, s, z), v / 6)
            for z, v in zip(x, w)
            for r, s in [(D(1) / 6, D(1) / 6), (D(2) / 3, D(1) / 6), (D(1) / 6, D(2) / 3)]
        ]
    return [(tuple(x[i] for i in p), w[p[0]] * w[p[1]] * w[p[2]]) for p in product(range(3), repeat=3)]


def inverse(j):
    # Cyclic minor indexing already carries its cofactor sign.
    cof = [
        [
            j[(r + 1) % 3][(c + 1) % 3] * j[(r + 2) % 3][(c + 2) % 3]
            - j[(r + 1) % 3][(c + 2) % 3] * j[(r + 2) % 3][(c + 1) % 3]
            for c in range(3)
        ]
        for r in range(3)
    ]
    det = sum(j[0][c] * cof[0][c] for c in range(3))
    if det <= 0:
        raise ValueError("Invalid source Jacobian")
    return [[cof[c][r] / det for c in range(3)] for r in range(3)], det


@lru_cache(maxsize=256)
def stiffness(kind, coordinates, young, nu):
    with localcontext() as ctx:
        ctx.prec = 50
        p = [[v if isinstance(v, D) else D.from_float(v) for v in row] for row in coordinates]
        e, v = D.from_float(young), D.from_float(nu)
        mu = e / (2 * (1 + v))
        lam = e * v / ((1 + v) * (1 - 2 * v))
        n = len(p)
        k = [[D(0) for _ in range(3 * n)] for _ in range(3 * n)]
        variables, expression = expressions(kind)
        for point, weight in quadrature(kind):
            env = dict(zip(("xsi", "eta", "zeta"), point))
            for name, node in variables:
                env[name] = evaluate(node, env)
            shapes = evaluate(expression, env)
            dn = [row[1:] for row in shapes]
            jac = [[sum(dn[i][a] * p[i][b] for i in range(n)) for b in range(3)] for a in range(3)]
            inv, det = inverse(jac)
            grad = [[sum(inv[a][b] * dn[i][b] for b in range(3)) for a in range(3)] for i in range(n)]
            for i in range(n):
                for j in range(n):
                    dot = sum(a * b for a, b in zip(grad[i], grad[j]))
                    for a in range(3):
                        for b in range(3):
                            k[3 * i + a][3 * j + b] += (
                                weight
                                * det
                                * (
                                    lam * grad[i][a] * grad[j][b]
                                    + mu * grad[i][b] * grad[j][a]
                                    + (mu * dot if a == b else 0)
                                )
                            )
        return k


def build_system(path):
    source = read_source_records(path)
    nodes = source["nodes"]
    ids = sorted(nodes, key=int)
    offsets = {n: 3 * i for i, n in enumerate(ids)}
    rows = [{} for _ in range(3 * len(ids))]
    with localcontext() as ctx:
        ctx.prec = 50
        for el in source["elements"].values():
            kind = el["type"]
            if kind not in ("TetraElement2", "WedgeElement2", "HexaElement2"):
                raise ValueError("Quadratic solids only")
            p = [nodes[n] for n in el["nodes"]]
            # Exact Decimal subtraction before caching makes translation inert.
            origin = p[0]
            coords = tuple(tuple(D.from_float(v) - D.from_float(o) for v, o in zip(row, origin)) for row in p)
            mat = source["materials"][el["material"]]
            k = stiffness(kind, coords, mat["E"], mat["nu"])
            ix = [offsets[n] + a for n in el["nodes"] for a in range(3)]
            for i, di in enumerate(ix):
                for j, dj in enumerate(ix):
                    rows[di][dj] = rows[di].get(dj, D(0)) + k[i][j]
        high = [[[j, float(a)] for j, a in row.items()] for row in rows]
        low = [[[j, float(a - D.from_float(float(a)))] for j, a in row.items()] for row in rows]
    files = [Path(path).resolve(), ROOT / "docs/v0/src/SolidElement.js", Path(__file__).resolve()]
    prescribed = {
        str(offsets[n] + a): vals[2 * a + 1]
        for n, vals in source["restraints"].items()
        for a in range(3)
        if vals[2 * a]
    }
    return dict(
        source=source["path"],
        hashes={p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
        node_ids=ids,
        stiffness_rows=high,
        stiffness_rows_low=low,
        loads=[v for n in ids for v in source["loads"].get(n, [0] * 6)[:3]],
        displacement=[0.0] * (3 * len(ids)),
        prescribed=prescribed,
        reac={n: {} for n in source["restraints"]},
        maximum_free_force_residual=0,
        input_only=True,
    )
