"""Immutable, mesh-independent definitions for known-position spatial loads.

All coordinates and intensities retain the caller's units and precision. Path
orientation is preserved here; correspondence and geometric validation belong
to the geometry compiler, not to input normalization.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from collections.abc import Mapping
from math import dist, hypot, isfinite
from numbers import Integral, Real


def integer_id(value, label):
    if isinstance(value, bool) or not isinstance(value, (Integral, str)):
        raise ValueError(f'{label}: ID must be an integer')
    try:
        return int(value)
    except ValueError as error:
        raise ValueError(f'{label}: ID must be an integer') from error


def sequence(value, label):
    if isinstance(value, (str, bytes, Mapping, set, frozenset)):
        raise ValueError(f'{label}: expected a sequence')
    try:
        return tuple(value)
    except TypeError as error:
        raise ValueError(f'{label}: expected a sequence') from error


def finite_number(value, label):
    if isinstance(value, bool) or not isinstance(value, Real):
        raise ValueError(f'{label}: expected a finite number')
    try:
        result = float(value)
    except (OverflowError, ValueError) as error:
        raise ValueError(f'{label}: expected a finite number') from error
    if not isfinite(result):
        raise ValueError(f'{label}: expected a finite number')
    return result


def _ids(value, label):
    return tuple(integer_id(v, label) for v in sequence(value, label))


def _vector(value, label, *, unit=False):
    result = tuple(finite_number(v, label) for v in sequence(value, label))
    if len(result) != 3:
        raise ValueError(f'{label}: expected three components')
    if unit:
        scale = max(abs(v) for v in result)
        if scale == 0:
            raise ValueError(f'{label}: vector must be nonzero')
        scaled = tuple(v / scale for v in result)
        length = hypot(*scaled)
        result = tuple(v / length for v in scaled)
    return result


@dataclass(frozen=True)
class LocalPlane:
    """Explicit global origin and orthogonal axes; axis lengths carry no scale.

    Paths and mesh nodes always use global coordinates. A plane defines an
    isometric integration chart, not a transformation of input coordinates.
    """
    origin: tuple[float, float, float]
    axis_u: tuple[float, float, float]
    axis_v: tuple[float, float, float]

    def __post_init__(self):
        object.__setattr__(self, 'origin', _vector(self.origin, 'plane origin'))
        u = _vector(self.axis_u, 'plane axis_u', unit=True)
        v = _vector(self.axis_v, 'plane axis_v', unit=True)
        if abs(sum(a*b for a, b in zip(u, v))) > 1e-12:
            raise ValueError('plane axes must be orthogonal')
        object.__setattr__(self, 'axis_u', u)
        object.__setattr__(self, 'axis_v', v)


@dataclass(frozen=True)
class LoadDirection:
    """Unit global force direction, or normal from oriented panel connectivity."""
    mode: str = 'global'
    vector: tuple[float, float, float] | None = None

    def __post_init__(self):
        if self.mode not in ('global', 'normal'):
            raise ValueError('load direction mode must be global or normal')
        if self.mode == 'normal':
            if self.vector is not None:
                raise ValueError('normal load direction must not specify a vector')
        else:
            vector = (0., 0., 1.) if self.vector is None else self.vector
            object.__setattr__(self, 'vector', _vector(vector, 'load direction', unit=True))


@dataclass(frozen=True)
class GeometryTolerance:
    """Length tolerance only; never used to round or compare forces."""

    absolute_length: float = 1e-9
    relative_length: float = 1e-9

    def __post_init__(self):
        for name in ('absolute_length', 'relative_length'):
            value = finite_number(getattr(self, name), 'geometry tolerance')
            if value < 0:
                raise ValueError('geometry tolerance must be nonnegative')
            object.__setattr__(self, name, value)
        if self.absolute_length == self.relative_length == 0:
            raise ValueError('geometry tolerance must not be zero in both components')

    def length(self, representative_length: float) -> float:
        scale = finite_number(representative_length, 'geometry tolerance scale')
        if scale < 0:
            raise ValueError('geometry tolerance scale must be nonnegative')
        result = max(self.absolute_length, self.relative_length * scale)
        if not isfinite(result):
            raise ValueError('geometry tolerance scale overflows')
        return result


@dataclass(frozen=True)
class SpatialLoadPanel:
    id: int
    nodes: tuple[int, ...]
    elements: tuple[int, ...] = ()
    triangles: tuple[tuple[int, int, int], ...] = ()
    holes: tuple = ()
    tolerance: GeometryTolerance = field(default_factory=GeometryTolerance)
    plane: LocalPlane | None = None
    loading_nodes: tuple['SpatialLoadMeshNode', ...] = ()
    loading_triangles: tuple[tuple[int, int, int], ...] = ()

    def __post_init__(self):
        object.__setattr__(self, 'id', integer_id(self.id, 'panel'))
        label = f'panel {self.id}'
        nodes, elements = _ids(self.nodes, label), _ids(self.elements, label)
        triangles = tuple(_ids(t, label) for t in sequence(self.triangles, label))
        if len(nodes) < 3 or len(set(nodes)) != len(nodes):
            raise ValueError(f'{label}: at least three distinct structural nodes are required')
        if bool(elements) == bool(triangles):
            raise ValueError(f'{label}: specify either shell elements or explicit triangles')
        if len(set(elements)) != len(elements):
            raise ValueError(f'{label}: duplicate shell element ID')
        seen = set()
        for triangle in triangles:
            if len(triangle) != 3 or len(set(triangle)) != 3 or not set(triangle) <= set(nodes):
                raise ValueError(f'{label}: each triangle must use three distinct panel nodes')
            key = frozenset(triangle)
            if key in seen:
                raise ValueError(f'{label}: duplicate loading triangle')
            seen.add(key)
        holes = tuple(_ids(ring, label + ' hole') for ring in sequence(self.holes, label))
        if not isinstance(self.tolerance, GeometryTolerance):
            raise ValueError(f'{label}: tolerance must be GeometryTolerance')
        if self.plane is not None and not isinstance(self.plane, LocalPlane):
            raise ValueError(f'{label}: plane must be LocalPlane')
        loading_nodes = sequence(self.loading_nodes, label + ' loading nodes')
        if any(not isinstance(node, SpatialLoadMeshNode) for node in loading_nodes):
            raise ValueError(f'{label}: loading nodes must be SpatialLoadMeshNode records')
        loading_ids = [node.id for node in loading_nodes]
        if len(set(loading_ids)) != len(loading_ids):
            raise ValueError(f'{label}: duplicate loading node ID')
        loading_triangles = tuple(_ids(t, label + ' loading triangle')
                                  for t in sequence(self.loading_triangles, label))
        if bool(loading_nodes) != bool(loading_triangles):
            raise ValueError(f'{label}: loading nodes and loading triangles must be specified together')
        used, loading_seen = set(), set()
        for triangle in loading_triangles:
            if len(triangle) != 3 or len(set(triangle)) != 3 or not set(triangle) <= set(loading_ids):
                raise ValueError(f'{label}: loading triangle must use three distinct loading nodes')
            key = frozenset(triangle)
            if key in loading_seen:
                raise ValueError(f'{label}: duplicate independent loading triangle')
            loading_seen.add(key)
            used.update(triangle)
        if loading_nodes and used != set(loading_ids):
            raise ValueError(f'{label}: unused loading nodes')
        hole_universe = set(loading_ids) if loading_nodes else set(nodes)
        hole_nodes = set()
        for ring in holes:
            if len(ring) < 3 or len(set(ring)) != len(ring) or not set(ring) <= hole_universe:
                raise ValueError(f'{label}: each hole must use distinct panel or loading nodes')
            if hole_nodes & set(ring):
                raise ValueError(f'{label}: duplicate or touching holes')
            hole_nodes.update(ring)
        for name, value in (('nodes', nodes), ('elements', elements),
                            ('triangles', triangles), ('holes', holes)):
            object.__setattr__(self, name, value)
        object.__setattr__(self, 'loading_nodes', loading_nodes)
        object.__setattr__(self, 'loading_triangles', loading_triangles)


@dataclass(frozen=True)
class SpatialLoadPath:
    id: int
    points: tuple[tuple[float, float, float], ...]
    cumulative_lengths: tuple[float, ...] = field(init=False)

    def __post_init__(self):
        object.__setattr__(self, 'id', integer_id(self.id, 'path'))
        label = f'path {self.id}'
        points = tuple(tuple(finite_number(v, label) for v in sequence(p, label))
                       for p in sequence(self.points, label))
        if len(points) < 2 or any(len(p) != 3 for p in points):
            raise ValueError(f'{label}: at least two three-dimensional points are required')
        if len(set(points)) != len(points):
            raise ValueError(f'{label}: repeated points or zero-length segments')
        lengths = [0.]
        for a, b in zip(points, points[1:]):
            length = dist(a, b)
            total = lengths[-1] + length
            if not isfinite(total) or total <= lengths[-1]:
                raise ValueError(f'{label}: path length overflows or loses segment precision')
            lengths.append(total)
        object.__setattr__(self, 'points', points)
        object.__setattr__(self, 'cumulative_lengths', tuple(lengths))


@dataclass(frozen=True)
class SpatialLoadMeshNode:
    """A geometry-only loading node; it owns no structural DOF or stiffness."""
    id: int
    point: tuple[float, float, float]

    def __post_init__(self):
        object.__setattr__(self, 'id', integer_id(self.id, 'loading node'))
        object.__setattr__(self, 'point', _vector(self.point, f'loading node {self.id}'))


@dataclass(frozen=True)
class SpatialLoad:
    id: int
    panel_id: int
    path_ids: tuple[int, ...]
    end_intensities: tuple[tuple[float, float], ...]
    direction: LoadDirection = field(default_factory=LoadDirection)

    def __post_init__(self):
        object.__setattr__(self, 'id', integer_id(self.id, 'load'))
        label = f'load {self.id}'
        panel = integer_id(self.panel_id, label)
        paths = _ids(self.path_ids, label)
        values = tuple(tuple(finite_number(v, label) for v in sequence(pair, label))
                       for pair in sequence(self.end_intensities, label))
        if len(paths) not in (1, 2) or len(set(paths)) != len(paths):
            raise ValueError(f'{label}: one or two distinct path IDs are required')
        if len(values) != len(paths) or any(len(pair) != 2 for pair in values):
            raise ValueError(f'{label}: two endpoint intensities are required per path')
        if not isinstance(self.direction, LoadDirection):
            raise ValueError(f'{label}: direction must be LoadDirection')
        object.__setattr__(self, 'panel_id', panel)
        object.__setattr__(self, 'path_ids', paths)
        object.__setattr__(self, 'end_intensities', values)

    @property
    def feature(self):
        return 'spatial_line' if len(self.path_ids) == 1 else 'spatial_area'


@dataclass(frozen=True)
class SpatialLoadDefinitions:
    panels: tuple[SpatialLoadPanel, ...] = ()
    paths: tuple[SpatialLoadPath, ...] = ()
    loads: tuple[SpatialLoad, ...] = ()

    def __post_init__(self):
        for name, cls in (('panels', SpatialLoadPanel), ('paths', SpatialLoadPath),
                          ('loads', SpatialLoad)):
            records = sequence(getattr(self, name), name)
            seen = set()
            for record in records:
                if not isinstance(record, cls):
                    raise ValueError(f'{name}: expected {cls.__name__}')
                if record.id in seen:
                    raise ValueError(f'duplicate {name[:-1]} ID {record.id}')
                seen.add(record.id)
            object.__setattr__(self, name, records)
        panels = {p.id for p in self.panels}
        paths = {p.id for p in self.paths}
        for load in self.loads:
            if load.panel_id not in panels:
                raise ValueError(f'load {load.id}: missing panel {load.panel_id}')
            for path in load.path_ids:
                if path not in paths:
                    raise ValueError(f'load {load.id}: missing path {path}')

    @property
    def has_definitions(self):
        return bool(self.panels or self.paths or self.loads)
