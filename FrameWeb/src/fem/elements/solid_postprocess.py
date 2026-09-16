"""Small-strain stress recovery in global coordinates."""
import numpy as np


def solid_stress_strain(element, displacement):
    coords = element.get_element_coordinates()
    points = (np.array([[.25,.25,.25]]) if len(coords) == 4
              else element.get_gauss_points()[0])
    u = np.asarray(displacement).reshape(len(coords), 3)
    strains = []
    for point in points:
        derivatives = element.get_shape_derivatives(point)
        gradient = np.linalg.solve(derivatives@coords, derivatives)@u
        strains.append([gradient[0,0],gradient[1,1],gradient[2,2],
                        gradient[0,1]+gradient[1,0],gradient[1,2]+gradient[2,1],
                        gradient[0,2]+gradient[2,0]])
    strains = np.asarray(strains)
    elastic = element.material.get_elastic_matrix_3d(element.material_id)
    return dict(gauss_points=points, strain=strains, stress=strains@elastic.T)
