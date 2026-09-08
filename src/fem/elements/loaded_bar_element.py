"""Linear beam boundary-value solution, with consistent loads and releases.

The state transition integrates equilibrium and compatibility exactly on each
constant-property segment. No penalty stiffness is used for hinges/foundations.
"""
import numpy as np
from scipy.linalg import expm

from .bar_element import BEBarElement


def boundary_map(length, rigidity, foundation=0., bending=False, q=(0., 0.), shear_rigidity=None):
    """Return end stiffness and consistent load from an independent ODE BVP.

    Axial state (u,N): u'=N/EA, N'=k*u-q.
    Bending state (v,theta,M,V): v'=theta, theta'=M/EI,
    M'=V, V'=q-k*v. End actions are (V_i,-M_i,-V_j,M_j).
    Two extra states represent q(x)=q_i+(q_j-q_i)*x/L.
    """
    n = 4 if bending else 2
    a = np.zeros((n+2, n+2))
    if bending:
        a[0, 1], a[1, 2], a[2, 3] = 1, 1/rigidity, 1
        if shear_rigidity is not None:
            a[0, 3] = -1/shear_rigidity
        a[3, 0], a[3, n] = -foundation, 1
        displacement = [0, 1]
        force = [3, 2]
        signs_i, signs_j = [1, -1], [-1, 1]
    else:
        a[0, 1], a[1, 0], a[1, n] = 1/rigidity, foundation, -1
        displacement, force = [0], [1]
        signs_i, signs_j = [-1], [1]
    a[n, n+1] = 1
    transition = expm(a*length)
    t = transition[:n, :n]
    offset = transition[:n, n:] @ [q[0], (q[1]-q[0])/length]
    c = np.vstack([np.eye(n)[displacement], t[displacement]])
    h = np.r_[np.zeros(len(displacement)), offset[displacement]]
    g = np.vstack([np.eye(n)[force]*np.array(signs_i)[:, None],
                   t[force]*np.array(signs_j)[:, None]])
    f0 = np.r_[np.zeros(len(force)), offset[force]*signs_j]
    k = np.linalg.solve(c.T, g.T).T
    fixed = f0-k@h
    return (k+k.T)/2, -fixed


class LoadedBarElement(BEBarElement):
    """Linear beam with optional shear, foundation, loads and end releases."""
    def __init__(self, *args, releases=(), foundation=(0., 0., 0., 0.), shear_correction=False, **kwargs):
        super().__init__(*args, **kwargs)
        self.releases = sorted(set(releases))
        self.foundation = np.asarray(foundation, dtype=float)
        self.shear_correction = shear_correction
        self.line_load = np.zeros((4, 2))
        self.temperature_strain = 0.
        self.load_factor = 1.
        self._cache = None

    def set_node_coordinates(self, coordinates):
        super().set_node_coordinates(coordinates)
        self._cache = None

    def set_line_load(self, direction, values):
        directions = {'x': 'Lx', 'y': 'Ly', 'z': 'Lz', 'r': 'Lr',
                      'gx': 'GX', 'gy': 'GY', 'gz': 'GZ'}
        direction = directions.get(direction, direction)
        if direction in ('GX', 'GY', 'GZ'):
            loads = np.zeros((3, 2))
            loads[('GX', 'GY', 'GZ').index(direction)] = values
            self.line_load[:3] += self.transformation_matrix @ loads
        elif direction in ('Lx', 'Ly', 'Lz', 'Lr'):
            self.line_load[('Lx', 'Ly', 'Lz', 'Lr').index(direction)] += values
        else:
            raise ValueError(f'Unknown beam load direction: {direction}')
        self._cache = None

    def _local_system(self):
        if self._cache is not None:
            return self._cache
        m = self.material.materials[self.material_id]
        p = self.bar_param
        k, f = np.zeros((12, 12)), np.zeros(12)
        modes = [(0, [0, 6], m.E*p.area, False, [1, 1]),
                 (1, [1, 5, 7, 11], m.E*p.Iz, True, [1, 1, 1, 1]),
                 (2, [2, 4, 8, 10], m.E*p.Iy, True, [1, -1, 1, -1]),
                 (3, [3, 9], m.G*p.J, False, [1, 1])]
        for mode, indices, rigidity, bending, signs in modes:
            if rigidity == 0 and not self.foundation[mode] and not np.any(self.line_load[mode]):
                continue
            if rigidity <= 0:
                raise ValueError('A loaded or supported beam mode requires positive rigidity')
            km, fm = boundary_map(self.length, rigidity, self.foundation[mode], bending,
                                   self.line_load[mode],
                                   m.G*p.area*(p.kappa_y if mode == 1 else p.kappa_z)
                                   if bending and self.shear_correction else None)
            signs = np.array(signs)
            k[np.ix_(indices, indices)] = km*signs[:, None]*signs[None, :]
            f[indices] = fm*signs
        f[[0, 6]] += m.E*p.area*self.temperature_strain*np.array([-1, 1])
        if self.releases:
            r = self.releases
            # Released rotations are internal unknowns; static condensation
            # applies to the load vector as well as the stiffness.
            block = k[np.ix_(r, r)]
            inverse = np.linalg.pinv(block, rcond=1e-13)
            if not np.allclose(block@inverse@f[r], f[r], rtol=1e-10, atol=1e-12):
                raise ValueError('Unbalanced load on released beam mode')
            f = f-k[:, r]@inverse@f[r]
            k = k-k[:, r]@inverse@k[r, :]
            k[r, :], k[:, r], f[r] = 0., 0., 0.
        self._cache = k, f
        return self._cache

    def get_stiffness_matrix(self):
        k, _ = self._local_system()
        t = self.get_transformation_matrix(12)
        return t.T@k@t

    def get_member_load_vector(self):
        return self.get_transformation_matrix(12).T@self._local_system()[1]

    def calculate_forces(self, displacement):
        k, f = self._local_system()
        local = k@(self.get_transformation_matrix(12)@displacement)-self.load_factor*f
        return dict(i_end=local[:6].copy(), j_end=local[6:].copy())
