"""Discrete Kirchhoff triangle rotation interpolation from V0 ShellElement.js.

TriElement1.shapeFunction3 / strainMatrix2, expressed in a planar right-handed
frame. DOFs are physical [w, rx, ry]; curvature is [ry,x, -rx,y, ry,y-rx,x].
This thin-plate model neglects transverse shear deformation.
"""
import numpy as np


def curvature_matrix(point, coords):
    r, s = point
    z = 1-r-s
    quadratic = np.array([[z*(2*z-1), 1-4*z, 1-4*z],
                          [r*(2*r-1), 4*r-1, 0],
                          [s*(2*s-1), 0, 4*s-1],
                          [4*z*r, 4*(z-r), -4*r],
                          [4*r*s, 4*s, 4*r],
                          [4*z*s, -4*s, 4*(z-s)]])
    edges = np.roll(coords, -1, axis=0)-coords
    x, y = edges.T
    inverse_length2 = 1/np.sum(edges*edges, axis=1)
    a = 3*inverse_length2*y/2
    b = -3*inverse_length2*x/2
    c = (3*inverse_length2*y*y-2)/4
    d = 3*inverse_length2*x*y/4
    e = (1-3*inverse_length2*y*y)/4
    # rotation shape functions: [DOF, rx/ry, value/d_dr/d_ds]
    h = np.zeros((9, 2, 3), dtype=coords.dtype)
    for i in range(3):
        prev = (i-1)%3
        h[3*i, 0] = a[prev]*quadratic[3+prev]-a[i]*quadratic[3+i]
        h[3*i, 1] = b[prev]*quadratic[3+prev]-b[i]*quadratic[3+i]
        h[3*i+1, 0] = quadratic[i]-c[prev]*quadratic[3+prev]-c[i]*quadratic[3+i]
        h[3*i+1, 1] = d[prev]*quadratic[3+prev]+d[i]*quadratic[3+i]
        h[3*i+2, 0] = h[3*i+1, 1]
        h[3*i+2, 1] = quadratic[i]-e[prev]*quadratic[3+prev]-e[i]*quadratic[3+i]
    jacobian = np.array([coords[1]-coords[0], coords[2]-coords[0]])
    a, b = jacobian[0]; c, d = jacobian[1]
    inverse = np.array([[d,-b],[-c,a]])/(a*d-b*c)
    gradients = (inverse@h[:, :, 1:].reshape(-1, 2).T).T.reshape(9, 2, 2)
    curvature = np.array([gradients[:, 1, 0], -gradients[:, 0, 1],
                          gradients[:, 1, 1]-gradients[:, 0, 0]])
    result = np.zeros((3, 18), dtype=coords.dtype)
    result[:, [6*i+j for i in range(3) for j in (2, 3, 4)]] = curvature
    return result
