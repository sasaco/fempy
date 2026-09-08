"""Independent equilibrium reference using only original V0 JS stiffness.

No production FEM imports or fixture writes. Keep the original-output reaction
candidate separate from a newly solved source model, with dependency hashes.
"""
import hashlib
import json
import math
from pathlib import Path
import subprocess

import numpy as np
from scipy.sparse import csr_matrix, diags
from scipy.sparse.linalg import splu


def precise_dot(row, high, low):
    terms = []
    for j, a in row:
        b = float(high[j]); product = a*b
        if hasattr(math, 'fma'):
            error = math.fma(a, b, -product)
        else:
            from decimal import Decimal, localcontext
            with localcontext() as context:
                context.prec = 80
                error = float(Decimal.from_float(a)*Decimal.from_float(b)-Decimal.from_float(product))
        terms.extend((product, error, a*low[j]))
    return math.fsum(terms)


def solve_source(source, *, input_only=False, decimal_stiffness=False):
    script = Path(__file__).with_name('v0_support_reference.cjs')
    if decimal_stiffness:
        from v0_decimal_reference import build_system
        raw = build_system(source)
        input_only = True
    else:
        raw = json.loads(subprocess.check_output(
            ['node', str(script), str(source), '--system']+(['--input-only'] if input_only else []),
            text=True, encoding='utf8'))
    rows = raw.pop('stiffness_rows')
    low_rows = raw.pop('stiffness_rows_low', None)
    indices, values, offsets = [], [], [0]
    for row in rows:
        for j, value in row:
            indices.append(j); values.append(value)
        offsets.append(len(values))
    matrix = csr_matrix((values, indices, offsets), shape=(len(rows), len(rows)))
    load = np.array(raw.pop('loads'), dtype=float)
    u = np.array(raw.pop('displacement'), dtype=float); low = np.zeros_like(u)
    prescribed = {int(i):v for i,v in raw.pop('prescribed').items()}
    for i, value in prescribed.items(): u[i] = value
    free = np.array([i for i in range(len(u)) if i not in prescribed], dtype=int)
    if len(free):
        k = matrix[free][:, free]
        scale = 1/np.sqrt(k.diagonal())
        lu = splu((diags(scale)@k@diags(scale)).tocsc())
        if np.min(abs(lu.U.diagonal())) <= np.finfo(float).eps*len(free):
            raise ValueError('Source stiffness is numerically singular')
    for iteration in range(16):
        # fsum prevents residual cancellation during high-accuracy refinement.
        internal = np.array([precise_dot(row, u, low) for row in rows])
        if low_rows is not None:
            internal += np.array([precise_dot(row, u, low) for row in low_rows])
        residual = load-internal
        maximum = max(abs(residual[free]), default=0.)
        if maximum <= 1e-11*max(1., max(abs(load))): break
        delta = scale*lu.solve(scale*residual[free])+low[free]
        high = u[free]+delta; part = high-u[free]
        low[free] = (u[free]-(high-part))+(delta-part); u[free] = high
    else:
        raise ValueError(f'Source reference refinement did not converge: {maximum}')
    names = ('tx','ty','tz')
    reaction = {}
    for n, node in enumerate(raw['node_ids']):
        if node not in raw['reac']: continue
        reaction[node] = {name:float(internal[3*n+j]-load[3*n+j]) if 3*n+j in prescribed else 0.
                          for j,name in enumerate(names)}
        reaction[node].update(mx=0.,my=0.,mz=0.)
    if input_only:
        raw.pop('reac'); raw.pop('maximum_free_force_residual')
        raw['disg'] = {node:dict(zip(('dx','dy','dz','rx','ry','rz'),
            [float(v) for v in u[3*n:3*n+3]+low[3*n:3*n+3]]+[0.,0.,0.]))
            for n,node in enumerate(raw['node_ids'])}
    else:
        raw['original_reac'] = raw.pop('reac')
        raw['original_maximum_free_force_residual'] = raw.pop('maximum_free_force_residual')
    raw.update(reac=reaction, maximum_free_force_residual=maximum, refinement_iterations=iteration,
               method='Independent equilibrated solve of original V0 JS stiffness; compensated residual refinement')
    if decimal_stiffness:
        raw['method'] = 'Original V0 shape expressions evaluated at 50 decimal digits; two-part stiffness assembly and compensated equilibrium solve'
    for path in (script, Path(__file__)):
        raw['hashes']['tests/'+path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
    return raw
