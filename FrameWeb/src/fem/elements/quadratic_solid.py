"""Isoparametric tetra10, wedge15 and serendipity hexa20 linear solids.

Node order and stiffness quadrature follow docs/v0/src/SolidElement.js.
The nodal polynomial basis defines the shape functions exactly; no midside
nodes are dropped. Small-strain isotropic elasticity, three translations/node.
"""
from functools import lru_cache
from itertools import product
import numpy as np
from .solid_element import SolidElementBase


NODES = {
    'tetra2': [[0,0,0],[1,0,0],[0,1,0],[0,0,1],[.5,0,0],[.5,.5,0],
               [0,.5,0],[0,0,.5],[.5,0,.5],[0,.5,.5]],
    'wedge2': [[0,0,-1],[1,0,-1],[0,1,-1],[0,0,1],[1,0,1],[0,1,1],
               [.5,0,-1],[.5,.5,-1],[0,.5,-1],[.5,0,1],[.5,.5,1],[0,.5,1],
               [0,0,0],[1,0,0],[0,1,0]],
    'hexa2': [[-1,-1,-1],[1,-1,-1],[1,1,-1],[-1,1,-1],
              [-1,-1,1],[1,-1,1],[1,1,1],[-1,1,1],
              [0,-1,-1],[1,0,-1],[0,1,-1],[-1,0,-1],
              [0,-1,1],[1,0,1],[0,1,1],[-1,0,1],
              [-1,-1,0],[1,-1,0],[1,1,0],[-1,1,0]],
}


@lru_cache(None)
def _basis(kind):
    powers = np.array([p for p in product(range(3), repeat=3) if
        (sum(p) <= 2 if kind == 'tetra2' else
         (p[0]+p[1] <= (1 if p[2] == 2 else 2)) if kind == 'wedge2' else
         sum(v for v in p if v > 1) <= 2)])
    nodes = np.asarray(NODES[kind], dtype=float)
    vandermonde = np.prod(nodes[:,None,:]**powers[None,:,:], axis=2)
    return powers, np.linalg.inv(vandermonde)


class QuadraticSolidElement(SolidElementBase):
    def __init__(self, element_id, node_ids, material_id, kind):
        if kind not in NODES or len(node_ids) != len(NODES[kind]):
            raise ValueError(f'Invalid node count for {kind}')
        super().__init__(element_id, node_ids, material_id)
        self.kind = kind
        self.material = None

    def get_name(self):
        return self.kind

    def set_material_properties(self, material):
        self.material = material

    def get_shape_functions(self, xi):
        powers, coefficients = _basis(self.kind)
        return np.prod(np.asarray(xi)**powers, axis=1)@coefficients

    def get_shape_derivatives(self, xi):
        powers, coefficients = _basis(self.kind)
        derivatives = []
        for axis in range(3):
            exponent = powers.copy(); exponent[:, axis] = np.maximum(0, exponent[:, axis]-1)
            derivatives.append((powers[:,axis]*np.prod(np.asarray(xi)**exponent, axis=1))@coefficients)
        return np.array(derivatives)

    def get_gauss_points(self):
        if self.kind == 'tetra2':
            a, b = (5-np.sqrt(5))/20, (5+3*np.sqrt(5))/20
            return np.array([[a,a,a],[b,a,a],[a,b,a],[a,a,b]]), np.full(4, 1/24)
        x, w = np.polynomial.legendre.leggauss(3)
        if self.kind == 'wedge2':
            return (np.array([[r,s,z] for z in x for r,s in [(1/6,1/6),(2/3,1/6),(1/6,2/3)]]),
                    np.repeat(w/6, 3))
        indices = list(product(range(3), repeat=3))
        return np.array([[x[i] for i in p] for p in indices]), np.array([np.prod(w[list(p)]) for p in indices])

    def _gradient(self, xi):
        derivatives = self.get_shape_derivatives(xi)
        coords = self.get_element_coordinates()
        jacobian = derivatives@(coords-coords[0])
        det = np.linalg.det(jacobian)
        if not np.isfinite(det) or det <= 1e-12*np.linalg.norm(jacobian)**3:
            raise ValueError(f'Invalid Jacobian in {self.kind} element {self.element_id}')
        return np.linalg.solve(jacobian, derivatives), det

    def get_stiffness_matrix(self):
        return self.get_stiffness_matrix_parts()[0].copy()

    def get_stiffness_matrix_parts(self):
        from decimal import Decimal, localcontext
        from .solid_precision import stiffness_parts
        coords=self.get_element_coordinates()
        # Preserve the existing scaled Jacobian validity check.
        for point in self.get_gauss_points()[0]: self._gradient(point)
        with localcontext() as context:
            context.prec=50
            origin=[Decimal.from_float(float(v)) for v in coords[0]]
            relative=tuple(tuple(Decimal.from_float(float(v))-o for v,o in zip(row,origin)) for row in coords)
        material=self.material.materials[self.material_id]
        return stiffness_parts(self.kind,relative,material.E,material.nu)

    def get_mass_matrix(self):
        density = self.material.materials[self.material_id].density
        if density is None or not np.isfinite(density) or density < 0:
            raise ValueError('A finite nonnegative density is required for mass')
        # Degree-four shape products need higher integration than stiffness
        # in a tetrahedron. Positive Duffy quadrature also covers wedge mass.
        x, w = np.polynomial.legendre.leggauss(4)
        unit, unit_w = (x+1)/2, w/2
        mass = np.zeros((len(self.node_ids),)*2)
        for i,j,k in product(range(4), repeat=3):
            if self.kind == 'hexa2':
                point, weight = [x[i],x[j],x[k]], w[i]*w[j]*w[k]
            elif self.kind == 'wedge2':
                point = [unit[i],(1-unit[i])*unit[j],x[k]]
                weight = unit_w[i]*unit_w[j]*w[k]*(1-unit[i])
            else:
                point = [unit[i],(1-unit[i])*unit[j],(1-unit[i])*(1-unit[j])*unit[k]]
                weight = unit_w[i]*unit_w[j]*unit_w[k]*(1-unit[i])**2*(1-unit[j])
            n = self.get_shape_functions(point)
            mass += density*self._gradient(point)[1]*weight*np.outer(n,n)
        return np.kron(mass, np.eye(3))

    def calculate_stress_strain(self, displacement):
        u = np.asarray(displacement, dtype=float)
        if u.shape != (3*len(self.node_ids),) or not np.isfinite(u).all():
            raise ValueError('Solid output requires one finite displacement per DOF')
        u = u.reshape(-1,3); u = u-u[0]
        points, _ = self.get_gauss_points()
        strains = []
        for point in points:
            g = self._gradient(point)[0]@u
            strains.append([g[0,0],g[1,1],g[2,2],g[0,1]+g[1,0],g[1,2]+g[2,1],g[0,2]+g[2,0]])
        strains = np.array(strains)
        return dict(gauss_points=points, strain=strains, stress=strains@self.get_stress_strain_matrix().T)
