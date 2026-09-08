"""Independent DKT reference from original JS operators and tensor algebra.

Uses original shape expressions, but not its buggy elementStrainStress:
integrate both surfaces separately and rotate tensors before engineering shear.
"""
import hashlib
import json
from pathlib import Path
import subprocess

import numpy as np
from scipy.sparse import csr_matrix, diags
from scipy.sparse.linalg import splu

from v0_refined_reference import precise_dot


def solve_source(source):
    script = Path(__file__).with_suffix('.cjs')
    raw = json.loads(subprocess.check_output(['node',str(script),str(source)],text=True,encoding='utf8'))
    rows = raw.pop('stiffness_rows'); n = len(rows)
    rr, cc, vv = [], [], []
    for i,row in enumerate(rows):
        for j,v in row: rr.append(i); cc.append(j); vv.append(v)
    K = csr_matrix((vv,(rr,cc)),shape=(n,n))
    F = np.array(raw.pop('loads')); fixed = {int(i):v for i,v in raw.pop('prescribed').items()}
    u = np.zeros(n); low = np.zeros(n)
    for i,v in fixed.items(): u[i] = v
    free = np.array([i for i in range(n) if i not in fixed])
    k = K[free][:,free]; scale = 1/np.sqrt(k.diagonal())
    lu = splu((diags(scale)@k@diags(scale)).tocsc())
    if min(abs(lu.U.diagonal())) <= np.finfo(float).eps*len(free):
        raise ValueError('Singular original DKT model')
    for iteration in range(12):
        internal = np.array([precise_dot(row,u,low) for row in rows])
        residual = F-internal
        if max(abs(residual[free]),default=0.) < 1e-11: break
        change = scale*lu.solve(scale*residual[free])+low[free]
        high = u[free]+change; part = high-u[free]
        low[free] = (u[free]-(high-part))+(change-part); u[free] = high
    else: raise ValueError('Independent DKT residual did not converge')
    displacement = u+low
    disg = {node:dict(zip(('dx','dy','dz','rx','ry','rz'),displacement[6*i:6*i+6].tolist()))
            for i,node in enumerate(raw['node_ids'])}
    reac = {node:{key:float(internal[6*i+j]-F[6*i+j]) if 6*i+j in fixed else 0.
                  for j,key in enumerate(('tx','ty','tz','mx','my','mz'))}
            for i,node in enumerate(raw['node_ids']) if node in raw['restraint_nodes']}
    shell_results = {}
    for index, op in enumerate(raw.pop('operators')):
        values = displacement[op['indices']]; D = np.array(op['elastic']); basis = np.array(op['basis'])
        fields = {}
        for side in (1,2):
            strains, stresses, energies = [], [], []
            for point in op['fields']:
                eps = values@np.array(point[side-1]); sig = D@eps
                energy = .5*eps@sig
                def tensor(v, factor):
                    a = np.array([[v[0],v[2]/factor,0],[v[2]/factor,v[1],0],[0,0,0]])
                    a = basis@a@basis.T
                    out = a[[0,1,2,0,1,2],[0,1,2,1,2,0]]
                    out[3:] *= factor
                    return out
                strains.append(tensor(eps,2)); stresses.append(tensor(sig,1)); energies.append(energy)
            for name,v in [('Strain',strains),('Stress',stresses),('Energy',energies)]:
                fields[f'node{name}{side}'] = np.asarray(v[:3]).tolist()
                fields[f'elem{name}{side}'] = np.mean(v[3:],axis=0).tolist()
        shell_results[str(index)] = dict(raw_result=fields,strain_energy=fields['elemEnergy1'],
            stress=[dict(zip(('mx','my','mxy','qx','qy'),[v[0],v[1],v[3],v[4],v[5]])) for v in fields['nodeStress1']],
            strain=[dict(zip(('ex','ey','exy'),[v[0],v[1],v[3]])) for v in fields['nodeStrain1']])
    result = dict(disg=disg,reac=reac,size=len(disg),fsec={},shell_results=shell_results)
    for file in (script,Path(__file__),Path(__file__).with_name('v0_refined_reference.py')):
        raw['hashes']['tests/'+file.name] = hashlib.sha256(file.read_bytes()).hexdigest()
    return result, dict(hashes=raw['hashes'],maximum_free_force_residual=float(max(abs(residual[free]),default=0.)),
        method='Original V0 DKT stiffness/strain operators; independent equilibrated solve and two-surface tensor integration')
