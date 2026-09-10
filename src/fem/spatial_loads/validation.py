"""Mesh-reference validation; geometric compilation is implemented separately."""
from .definitions import SpatialLoadDefinitions


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
