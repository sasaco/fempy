"""Unit-invariant norms for generalized structural vectors.

Translations and rotations, or forces and moments, cannot be combined in a
Euclidean norm before they share a dimension.  These helpers use the model's
geometric span as the characteristic length.  A moment is divided by that
length before it is combined with forces; a translation is divided by the
same length before it is combined with rotations.
"""

from __future__ import annotations

import numpy as np


def characteristic_length(mesh) -> float | None:
    """Return the positive geometry span, or ``None`` for a point model."""
    coordinates = np.asarray(list(mesh.nodes.values()), dtype=float)
    if coordinates.ndim != 2 or coordinates.shape[0] == 0:
        return None
    if coordinates.shape[1] != 3 or not np.all(np.isfinite(coordinates)):
        raise ValueError("Node coordinates must be finite three-component vectors")
    length = float(np.linalg.norm(np.ptp(coordinates, axis=0)))
    if not np.isfinite(length):
        raise ValueError("Convergence scaling requires finite node coordinates")
    if length <= 0:
        return None
    return length


def _dof_indices(size: int, dof_indices) -> np.ndarray:
    if dof_indices is None:
        return np.arange(size, dtype=int)
    indices = np.asarray(dof_indices, dtype=int)
    if indices.shape != (size,):
        raise ValueError("dof_indices must match the vector length")
    return indices


def generalized_force_norm(
    values, *, stride: int, length: float, dof_indices=None
) -> float:
    """L2 norm in equivalent-force units: ``[F, M/L]``."""
    if length is None or not np.isfinite(length) or length <= 0:
        raise ValueError("Generalized force norm requires a positive characteristic length")
    vector = np.asarray(values, dtype=float)
    if vector.ndim != 1 or not np.all(np.isfinite(vector)):
        raise ValueError("Generalized force vector must be one-dimensional and finite")
    indices = _dof_indices(len(vector), dof_indices)
    scaled = vector.copy()
    rotational = indices % stride >= 3
    scaled[rotational] /= length
    return float(np.linalg.norm(scaled))


def generalized_displacement_norm(
    values, *, stride: int, length: float, dof_indices=None
) -> float:
    """Dimensionless L2 norm: ``[u/L, theta]``."""
    if length is None or not np.isfinite(length) or length <= 0:
        raise ValueError(
            "Generalized displacement norm requires a positive characteristic length"
        )
    vector = np.asarray(values, dtype=float)
    if vector.ndim != 1 or not np.all(np.isfinite(vector)):
        raise ValueError(
            "Generalized displacement vector must be one-dimensional and finite"
        )
    indices = _dof_indices(len(vector), dof_indices)
    scaled = vector.copy()
    translational = indices % stride < min(3, stride)
    scaled[translational] /= length
    return float(np.linalg.norm(scaled))


def relative_measure(numerator: float, reference: float) -> float:
    """Return a relative measure without a unit-dependent absolute floor."""
    if reference > 0:
        return numerator / reference
    return 0.0 if numerator == 0 else float("inf")
