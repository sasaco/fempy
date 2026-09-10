"""Input and geometry preparation without changing the mesh or load definitions."""
from dataclasses import dataclass

import numpy as np

from .definitions import SpatialLoadDefinitions
from .geometry import (
    PanelCell, PanelGeometry, PlanarFrame, clip_polygon, frozen_points,
    points_array, signed_area, split_line, triangulate_convex, validate_partition,
)
from .interpolation import build_strip, project_path


def validate_references(definitions, mesh):
    from ..capabilities import canonical_element_type

    if not isinstance(definitions, SpatialLoadDefinitions):
        raise ValueError('spatial_loads must be SpatialLoadDefinitions')
    structural_nodes = {n for e in mesh.elements.values() for n in e['nodes']}
    for panel in definitions.panels:
        for node in panel.nodes:
            if node not in mesh.nodes or node not in structural_nodes:
                raise ValueError(f'panel {panel.id}: missing structural node {node}')
        for element_id in panel.elements:
            element = mesh.elements.get(element_id)
            if element is None or canonical_element_type(element['type']) != 'shell':
                raise ValueError(f'panel {panel.id}: missing shell element {element_id}')
            if not set(element['nodes']) <= set(panel.nodes):
                raise ValueError(f'panel {panel.id}: shell {element_id} uses non-panel nodes')


def prepare_panel(panel, mesh):
    """Build a validated horizontal T3/Q4 partition from explicit connectivity."""
    label = f'panel {panel.id}'
    validate_references(SpatialLoadDefinitions(panels=(panel,)), mesh)
    vertices = points_array([mesh.nodes[n] for n in panel.nodes], 3, label)
    frame = PlanarFrame.from_points(vertices, panel.tolerance, label)
    if np.ptp(vertices[:, 2]) > frame.eps:
        raise ValueError(f'{label}: only global XY parallel panels are supported')
    # The initial public coordinate convention remains global XY and +Z.
    frame = PlanarFrame(frame.origin, (1., 0., 0.), (0., 1., 0.), (0., 0., 1.), frame.scale, frame.eps)
    projected = frame.project(vertices, label)
    for i, point in enumerate(projected):
        if any(np.linalg.norm(point - other) <= frame.eps for other in projected[i + 1:]):
            raise ValueError(f'{label}: coincident structural nodes')
    coordinates = dict(zip(panel.nodes, frozen_points(projected)))
    cells = []
    connectivity = ((eid, tuple(mesh.elements[eid]['nodes']), eid) for eid in panel.elements)
    if panel.triangles:
        connectivity = ((i, nodes, None) for i, nodes in enumerate(panel.triangles))
    for key, nodes, element_id in connectivity:
        if len(nodes) not in (3, 4) or len(set(nodes)) != len(nodes):
            raise ValueError(f'{label} cell {key}: only distinct T3/Q4 nodes are supported')
        cells.append(PanelCell(key, nodes, tuple(coordinates[n] for n in nodes), element_id))
    if {n for cell in cells for n in cell.node_ids} != set(panel.nodes):
        raise ValueError(f'{label}: unused panel nodes')
    cells = tuple(sorted(cells, key=lambda cell: cell.key))
    boundary = validate_partition(cells, frame.eps, label)
    return PanelGeometry(panel.id, frame, cells, boundary)


@dataclass(frozen=True)
class AreaPiece:
    cell_index: int
    strip_index: int
    triangle: tuple


@dataclass(frozen=True)
class PreparedLoad:
    """Geometry only. This does not enable analysis or contain assembled loads."""
    load: object
    panel: PanelGeometry
    paths: tuple
    line_pieces: tuple = ()
    strip_cells: tuple = ()
    area_pieces: tuple[AreaPiece, ...] = ()


def prepare_geometry(definitions, mesh):
    """Validate all panels and referenced load paths, returning immutable geometry.

    Public input acceptance remains separate until the load assembler and its
    numerical acceptance tests are connected to the solver preflight.
    """
    validate_references(definitions, mesh)
    panels = {p.id: prepare_panel(p, mesh) for p in definitions.panels}
    path_definitions = {p.id: p for p in definitions.paths}
    prepared = []
    for load in definitions.loads:
        panel = panels[load.panel_id]
        label = f'load {load.id} panel {load.panel_id}'
        paths = tuple(project_path(path_definitions[pid], panel, f'{label} path {pid}')
                      for pid in load.path_ids)
        if len(paths) == 1:
            pieces = split_line(panel, paths[0].points, paths[0].parameters, label)
            prepared.append(PreparedLoad(load, panel, paths, line_pieces=pieces))
            continue
        strips = build_strip(*paths, load.end_intensities, panel.frame.eps, label)
        pieces = []
        for strip_index, strip in enumerate(strips):
            triangles = []
            for cell_index, cell in enumerate(panel.cells):
                clipped = clip_polygon(strip.points, cell.points, panel.frame.eps)
                for triangle in triangulate_convex(clipped, panel.frame.eps):
                    pieces.append(AreaPiece(cell_index, strip_index, triangle))
                    triangles.append(triangle)
            integrated_area = sum(abs(signed_area(t)) for t in triangles)
            if abs(integrated_area - abs(signed_area(strip.points))) > panel.frame.eps * panel.frame.scale:
                raise ValueError(f'{label}: clipped area is incomplete or duplicated')
        prepared.append(PreparedLoad(load, panel, paths, strip_cells=strips, area_pieces=tuple(pieces)))
    return tuple(prepared)
