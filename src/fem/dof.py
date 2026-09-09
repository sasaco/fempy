"""One compact node/element layout for assembly, equilibrium and postprocessing."""

from dataclasses import dataclass, field

import numpy as np
from scipy.sparse import lil_matrix


SOLID_TYPES = frozenset((
    'tetra', 'hexa', 'wedge', 'tetra2', 'hexa2', 'wedge2',
    'TetraElement1', 'HexaElement1', 'WedgeElement1',
    'TetraElement2', 'HexaElement2', 'WedgeElement2',
))


@dataclass
class DofLayout:
    stride: int
    node_offsets: dict
    element_nodes: dict
    _indices: dict = field(default_factory=dict, init=False, repr=False)

    @staticmethod
    def stride_for(mesh):
        return 3 if all(e.get('type', 'bar') in SOLID_TYPES
                        for e in mesh.elements.values()) else 6

    @classmethod
    def from_mesh(cls, mesh):
        stride = cls.stride_for(mesh)
        return cls(stride, {n: i * stride for i, n in enumerate(sorted(mesh.nodes))},
                   {key: tuple(e['nodes']) for key, e in mesh.elements.items()})

    @property
    def size(self):
        return len(self.node_offsets) * self.stride

    def element_dofs(self, key, element):
        width = element.get_dof_per_node()
        if width > self.stride:
            raise ValueError('Element DOFs exceed the mesh layout')
        cache_key = (key, width)
        if cache_key not in self._indices:
            self._indices[cache_key] = [self.node_offsets[n] + i
                                        for n in self.element_nodes[key] for i in range(width)]
        return self._indices[cache_key]

    def elements(self, elements):
        for key in self.element_nodes:
            if key in elements:
                element = elements[key]
                yield key, element, self.element_dofs(key, element)

    def assemble_matrix(self, elements, evaluate):
        matrix = lil_matrix((self.size, self.size))
        for _, element, indices in self.elements(elements):
            values = evaluate(element, indices)
            for i, row in enumerate(indices):
                for j, column in enumerate(indices):
                    matrix[row, column] += values[i, j]
        return matrix.tocsr()

    @staticmethod
    def add_vector(target, indices, values):
        # add.at also handles repeated DOFs without dropping an accumulation.
        np.add.at(target, indices, values)
