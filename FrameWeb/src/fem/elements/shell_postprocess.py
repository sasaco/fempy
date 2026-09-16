"""Surface tensors and work-conjugate resultants for planar Mindlin shells.

Surface 1 is z=+t/2 along the node-order normal; surface 2 is z=-t/2.
Raw tensors are global [xx, yy, zz, xy, yz, zx], with TENSOR shear strain.
Energy1/2 are energy densities, not the integrated element energy. Transverse
stress is the constitutive (5/6)G*gamma used by the element, not a recovered
traction-free surface stress. Resultants are in the element local frame.
"""
import numpy as np


def shell_results(element, displacement, *, point_average=False):
    coords, basis = element._local_frame()
    u = np.asarray(displacement, dtype=float)
    if u.shape != (element.get_matrix_size(),) or not np.isfinite(u).all():
        raise ValueError('Shell output requires one finite displacement per DOF')
    t = element.thickness
    if not np.isfinite(t) or t <= 0:
        raise ValueError('Shell thickness must be finite and positive')
    local = (u.reshape(-1, 3)@basis.T).ravel()
    elastic = element.get_stress_strain_matrix()
    shear_modulus = (5/6)*element.material.materials[element.material_id].G
    kirchhoff_shear = None
    if element.formulation == 'dkt':
        from .dkt import curvature_matrix
        base = curvature_matrix((0., 0.), coords)@local
        natural = np.array([curvature_matrix(p, coords)@local-base for p in ((1.,0.), (0.,1.))])
        gradient = np.linalg.solve(np.array([coords[1]-coords[0], coords[2]-coords[0]]), natural)
        moment_gradient = t**3/12*gradient@elastic.T
        kirchhoff_shear = np.array([moment_gradient[0,0]+moment_gradient[1,2],
                                    moment_gradient[0,2]+moment_gradient[1,1]])

    def field(point):
        bm, bb, bs, det = element._strain_matrices(np.array(point), coords)
        if element.n_nodes == 4:
            bs = element._assumed_quad_shear(point, coords)
        return bm@local, bb@local, bs@local, det

    def surface(point, sign):
        membrane, curvature, shear, _ = field(point)
        strain = membrane+sign*t/2*curvature
        stress = elastic@strain
        transverse = shear_modulus*shear
        eps = np.array([[strain[0], strain[2]/2, shear[0]/2],
                        [strain[2]/2, strain[1], shear[1]/2],
                        [shear[0]/2, shear[1]/2, 0.]])
        sig = np.array([[stress[0], stress[2], transverse[0]],
                        [stress[2], stress[1], transverse[1]],
                        [transverse[0], transverse[1], 0.]])
        energy = .5*np.sum(eps*sig)
        eps, sig = basis.T@eps@basis, basis.T@sig@basis
        rows, cols = [0,1,2,0,1,2], [0,1,2,1,2,0]
        return eps[rows, cols], sig[rows, cols], energy

    vertices = ([[0.,0.], [1.,0.], [0.,1.]] if element.n_nodes == 3 else
                [[-1.,-1.], [1.,-1.], [1.,1.], [-1.,1.]])
    points, weights = element.get_gauss_points()
    if element.n_nodes == 3:
        points = np.array([[1/6,1/6], [2/3,1/6], [1/6,2/3]])
        weights = np.full(3, 1/6)
    measures = np.array([field(p)[3]*w for p, w in zip(points, weights)])
    area = measures.sum()
    raw = {}
    for side, sign in [(1, 1), (2, -1)]:
        nodal = [surface(p, sign) for p in vertices]
        integrated = [surface(p, sign) for p in points]
        for i, name in enumerate(['Strain', 'Stress', 'Energy']):
            raw[f'node{name}{side}'] = np.asarray([r[i] for r in nodal]).tolist()
            raw[f'elem{name}{side}'] = np.average(
                np.asarray([r[i] for r in integrated]), axis=0,
                weights=None if point_average else measures).tolist()
    membrane_force = np.zeros(3); moment = np.zeros(3); shear_force = np.zeros(2)
    physical_energy = 0.
    for point, measure in zip(points, measures):
        membrane, curvature, shear, _ = field(point)
        n, m = t*elastic@membrane, t**3/12*elastic@curvature
        q = t*shear_modulus*shear if kirchhoff_shear is None else kirchhoff_shear
        membrane_force += measure*n; moment += measure*m; shear_force += measure*q
        physical_energy += .5*measure*(membrane@n+curvature@m+shear@q)
    # Drilling is explicitly distinguished from physical strain energy.
    drilling_energy = sum(.5*measure*1e-3*element.material.materials[element.material_id].G*t
                          *float(element._drilling_strain(point, coords)@local)**2
                          for point, measure in zip(points, measures))
    # Physical cut tractions. Keep each element's shared edge separate; this
    # is not the undocumented legacy virtual-beam stiffness decomposition.
    edges = {}
    line_points, line_weights = np.polynomial.legendre.leggauss(2)
    for j in range(element.n_nodes):
        i = (j-1)%element.n_nodes
        tangent = coords[j]-coords[i]
        length = np.linalg.norm(tangent)
        normal = np.array([tangent[1], -tangent[0]])/length
        forces = np.zeros((2, 6))
        for xi, weight in zip(line_points, line_weights):
            shape = np.array([(1-xi)/2, (1+xi)/2])
            point = shape@np.asarray([vertices[i], vertices[j]])
            membrane, curvature, shear, _ = field(point)
            n, m = t*elastic@membrane, t**3/12*elastic@curvature
            q = t*shear_modulus*shear if kirchhoff_shear is None else kirchhoff_shear
            traction = np.array([[n[0],n[2]], [n[2],n[1]]])@normal
            bending = np.array([[m[0],m[2]], [m[2],m[1]]])@normal
            force = np.r_[traction, q@normal]@basis
            couple = np.array([-bending[1], bending[0], 0.])@basis
            forces += shape[:,None]*np.r_[force,couple]*length/2*weight
        edges[f'{element.node_ids[i]}-{element.node_ids[j]}'] = dict(
            node_ids=[element.node_ids[i],element.node_ids[j]],
            outward_normal=(np.r_[normal,0.]@basis).tolist(),
            i_end=forces[0].tolist(), j_end=forces[1].tolist())
    return dict(raw_result=raw, strain_energy=float(physical_energy),
                formulation=element.formulation,
                transverse_shear_recovery='moment_equilibrium' if kirchhoff_shear is not None else 'constitutive',
                drilling_energy=float(drilling_energy),
                edge_resultants=edges,
                resultants=dict(membrane=(membrane_force/area).tolist(),
                                moment=(moment/area).tolist(), shear=(shear_force/area).tolist()),
                local_basis=basis.tolist(), node_ids=list(element.node_ids),
                stress=[dict(zip(('mx','my','mxy','qx','qy'), [s[0],s[1],s[3],s[4],s[5]]))
                        for s in raw['nodeStress1']],
                strain=[dict(zip(('ex','ey','exy'), [s[0],s[1],2*s[3]]))
                        for s in raw['nodeStrain1']])


def legacy_shell_view(element, displacement):
    """Old field names with global engineering shear and point-mean density.

    src/fem/result_processor.py defines this schema. Physical tensors are
    rotated before conversion to engineering shear. Historical bugs that
    rotate engineering shear as a tensor, discard global components, or copy
    the top surface to the bottom are deliberately not reproduced.
    """
    result = shell_results(element, displacement, point_average=True)
    raw = result['raw_result']
    for side in (1, 2):
        for scope in ('node', 'elem'):
            key = f'{scope}Strain{side}'
            values = np.asarray(raw[key])
            values[..., 3:] *= 2
            raw[key] = values.tolist()
    return dict(stress=result['stress'], strain=result['strain'],
                strain_energy=raw['elemEnergy1'], raw_result=raw)
