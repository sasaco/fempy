"""Two-part quadratic stiffness from polynomial interpolation in Decimal.

Retaining the rounding tail matters when a small strain accompanies a large
rigid displacement. The public matrix remains float64; the solver also uses
its tail for equilibrium refinement. Cache translated copies of an element.
"""
from decimal import Decimal as D, localcontext
from functools import lru_cache
from itertools import product
import numpy as np


def _solve(a, b):
    """Three-row elimination in the caller's Decimal context."""
    rows=[list(row)+list(rhs) for row,rhs in zip(a,b)]
    determinant=D(1)
    for i in range(3):
        pivot=max(range(i,3),key=lambda j:abs(rows[j][i]))
        if rows[pivot][i] == 0: raise ValueError('Invalid quadratic-solid Jacobian')
        if pivot != i: rows[i],rows[pivot]=rows[pivot],rows[i];determinant=-determinant
        value=rows[i][i];determinant*=value
        rows[i]=[x/value for x in rows[i]]
        for j in range(3):
            if j != i:
                value=rows[j][i]
                rows[j]=[x-value*y for x,y in zip(rows[j],rows[i])]
    if determinant <= 0: raise ValueError('Invalid quadratic-solid Jacobian')
    return np.array([row[3:] for row in rows],dtype=object),determinant


@lru_cache(maxsize=128)
def stiffness_parts(kind, coordinates, young, poisson):
    from .quadratic_solid import _basis
    with localcontext() as ctx:
        ctx.prec=50
        powers,coefficients=_basis(kind)
        coefficients=np.array([[D.from_float(float(v)) for v in row] for row in coefficients],dtype=object)
        coords=np.array(coordinates,dtype=object)
        # Coordinates are Decimal differences, preserving the input floats.
        e,nu=D.from_float(float(young)),D.from_float(float(poisson))
        factor=e/((1+nu)*(1-2*nu))
        elastic=np.full((6,6),D(0),dtype=object)
        elastic[:3,:3]=nu*factor
        for i in range(3): elastic[i,i]=(1-nu)*factor
        for i in range(3,6): elastic[i,i]=e/(2*(1+nu))
        if kind == 'tetra2':
            a,b=(5-D(5).sqrt())/20,(5+3*D(5).sqrt())/20
            points=[(a,a,a),(b,a,a),(a,b,a),(a,a,b)];weights=[D(1)/24]*4
        else:
            x=[-(D(3)/5).sqrt(),D(0),(D(3)/5).sqrt()];w=[D(5)/9,D(8)/9,D(5)/9]
            if kind == 'wedge2':
                points=[(r,s,z) for z in x for r,s in [(D(1)/6,D(1)/6),(D(2)/3,D(1)/6),(D(1)/6,D(2)/3)]]
                weights=[v/6 for v in w for _ in range(3)]
            else:
                indices=list(product(range(3),repeat=3))
                points=[tuple(x[i] for i in p) for p in indices]
                weights=[w[p[0]]*w[p[1]]*w[p[2]] for p in indices]
        size=3*len(coords)
        k=np.full((size,size),D(0),dtype=object)
        for point,weight in zip(points,weights):
            derivatives=[]
            for axis in range(3):
                terms=[]
                for power in powers:
                    value=D(int(power[axis]))
                    for j in range(3):
                        exponent=int(power[j])-(1 if j==axis else 0)
                        if exponent > 0: value *= point[j]**exponent
                    terms.append(value)
                derivatives.append(np.array(terms,dtype=object)@coefficients)
            derivatives=np.array(derivatives,dtype=object)
            gradient,det=_solve(derivatives@coords,derivatives)
            bmat=np.full((6,size),D(0),dtype=object)
            for i,(dx,dy,dz) in enumerate(gradient.T):
                bmat[:,3*i:3*i+3]=[[dx,0,0],[0,dy,0],[0,0,dz],[dy,dx,0],[0,dz,dy],[dz,0,dx]]
            k += weight*det*(bmat.T@elastic@bmat)
        high=np.array(k,dtype=float)
        low=np.array([[v-D.from_float(float(h)) for v,h in zip(row,hr)] for row,hr in zip(k,high)],dtype=float)
        high.setflags(write=False);low.setflags(write=False)
        return high,low
