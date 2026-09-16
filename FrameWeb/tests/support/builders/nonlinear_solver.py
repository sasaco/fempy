"""Test Newton equilibrium independently of the JR constitutive implementation."""

import numpy as np

from fem.boundary_condition import BoundaryCondition
from fem.elements.bar_element import TBarElement
from fem.material import BarParameter, Material, MaterialProperty
from fem.mesh import MeshModel


def axial_bar(prescribed=None, spring=None):
    mesh = MeshModel()
    mesh.add_node(1, [0, 0, 0])
    mesh.add_node(2, [2, 0, 0])
    mesh.add_element(1, "bar", [1, 2], 1)
    material = Material()
    material.add_material(1, MaterialProperty("test", 2000, 0.25))
    element = TBarElement(1, [1, 2], 1, 1)
    element.set_node_coordinates(mesh.nodes)
    element.set_material_properties(material, BarParameter(3, 1, 1, 1))
    boundary = BoundaryCondition()
    boundary.add_restraint(1, [True] * 6)
    boundary.add_restraint(2, [False, True, True, True, True, True])
    if prescribed is not None:
        boundary.add_restraint(2, [True] * 6, [prescribed, 0, 0, 0, 0, 0])
    elif spring is not None:
        boundary.add_restraint(2, [True] * 6, [spring, 0, 0, 0, 0, 0])
    else:
        boundary.add_load(2, [30, 0, 0, 0, 0, 0])
    return mesh, material, boundary, {1: element}


class CubicBar:
    """Conservative spring: N = delta + delta**3 (not a JR-model mock)."""

    def __init__(self):
        self.committed = 0.0
        self.trial = 0.0
        self.rollbacks = 0

    def get_dof_per_node(self):
        return 6

    def get_internal_force(self, u):
        self.trial = u[6] - u[0]
        f = np.zeros(12)
        f[6] = self.trial + self.trial**3
        f[0] = -f[6]
        return f

    def get_tangent_stiffness_matrix(self, u):
        b = np.zeros(12)
        b[0], b[6] = -1, 1
        return (1 + 3 * (u[6] - u[0]) ** 2) * np.outer(b, b)

    def commit_state(self):
        self.committed = self.trial

    def rollback_state(self):
        self.trial = self.committed
        self.rollbacks += 1


class CappedBar(CubicBar):
    """Elastic/perfectly-plastic spring with a unit force capacity."""

    def get_internal_force(self, u):
        self.trial = u[6] - u[0]
        force = np.zeros(12)
        force[6] = np.clip(self.trial, -1, 1)
        force[0] = -force[6]
        return force

    def get_tangent_stiffness_matrix(self, u):
        b = np.zeros(12)
        b[0], b[6] = -1, 1
        return np.outer(b, b) if abs(u[6] - u[0]) <= 1 else np.zeros((12, 12))


class SofteningBar(CubicBar):
    """One-DOF envelope with a peak and a negative post-peak tangent."""

    @staticmethod
    def _response(delta):
        if delta <= 1:
            return delta, 1.0
        if delta < 2:
            return 2-delta, -1.0
        return 0.0, 0.0

    def get_internal_force(self, u):
        self.trial = u[6] - u[0]
        value, _ = self._response(self.trial)
        force = np.zeros(12)
        force[6], force[0] = value, -value
        return force

    def get_tangent_stiffness_matrix(self, u):
        b = np.zeros(12)
        b[0], b[6] = -1, 1
        _, tangent = self._response(u[6] - u[0])
        return tangent * np.outer(b, b)
