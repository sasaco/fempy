"""DKT polynomial stiffness with its rounding tail, at 50 decimal digits."""
from decimal import Decimal as D, localcontext
from functools import lru_cache
import numpy as np


@lru_cache(maxsize=128)
def stiffness_parts(coordinates, young, poisson, shear_modulus, thickness):
    from .dkt import curvature_matrix
    with localcontext() as ctx:
        ctx.prec = 50
        xyz = np.array(coordinates,dtype=object)
        def unit(v): return v/sum(v*v).sqrt()
        ex = unit(xyz[1]); ez = unit(np.cross(xyz[1],xyz[2])); ey = np.cross(ez,ex)
        basis = np.array([ex,ey,ez]); coords = (xyz@basis.T)[:,:2]
        E,nu,G,t = (D.from_float(float(v)) for v in (young,poisson,shear_modulus,thickness))
        elastic = E/(1-nu*nu)*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]],dtype=object)
        a,b = coords[1]; c,d = coords[2]; det = a*d-b*c
        gradient = np.array([[d,-b],[-c,a]],dtype=object)/det@np.array([[-1,1,0],[-1,0,1]])
        membrane = np.full((3,18),D(0),dtype=object)
        for i,(dx,dy) in enumerate(gradient.T):
            membrane[0,6*i] = dx; membrane[1,6*i+1] = dy
            membrane[2,6*i:6*i+2] = [dy,dx]
        stiffness = np.full((18,18),D(0),dtype=object)
        for r,s in ((D(1)/6,D(1)/6),(D(2)/3,D(1)/6),(D(1)/6,D(2)/3)):
            bend = curvature_matrix((r,s),coords)
            drill = np.full(18,D(0),dtype=object)
            drill[0::6] = gradient[1]/2; drill[1::6] = -gradient[0]/2
            drill[5::6] = [1-r-s,r,s]
            stiffness += det/6*(t*membrane.T@elastic@membrane + t**3/12*bend.T@elastic@bend
                                + G*t/1000*np.outer(drill,drill))
        transform = np.kron(np.eye(6,dtype=int),basis)
        stiffness = transform.T@stiffness@transform
        high = np.array(stiffness,dtype=float)
        low = np.array([[v-D.from_float(float(h)) for v,h in zip(row,hr)]
                        for row,hr in zip(stiffness,high)],dtype=float)
        high.setflags(write=False); low.setflags(write=False)
        return high,low
