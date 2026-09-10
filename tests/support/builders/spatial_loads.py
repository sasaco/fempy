"""Small legacy beam grid with explicit loading topology, independent of shells."""


def geometry_model(points=None, cells=None, *, shell=False, reverse=False, tolerance=None):
    """Build topology for geometry tests, without materials or a solver."""
    from fem.mesh import MeshModel
    from fem.spatial_loads import GeometryTolerance, SpatialLoadPanel

    points = points if points is not None else [(0, 0), (2, 0), (2, 2), (0, 2)]
    cells = cells if cells is not None else ([(1, 2, 3, 4)] if shell else [(1, 2, 3), (1, 3, 4)])
    cells = [tuple(reversed(c)) if reverse else tuple(c) for c in cells]
    mesh = MeshModel()
    for i, point in enumerate(points, 1):
        mesh.add_node(i, (*point, 0) if len(point) == 2 else point)
    for i, cell in enumerate(cells, 1):
        mesh.add_element(i, 'shell' if shell else 'bar', list(cell), 1)
    panel = SpatialLoadPanel(7, tuple(mesh.nodes),
                             elements=tuple(mesh.elements) if shell else (),
                             triangles=() if shell else tuple(cells),
                             tolerance=tolerance or GeometryTolerance())
    return mesh, panel


def legacy_panel(*, area=False):
    record = dict(L1="1", P11=10, P12=20)
    if area:
        record.update(L2="2", P21=30, P22=40)
    return dict(
        node={str(i): dict(x=x, y=y, z=0) for i, (x, y) in enumerate(
            ((0, 0), (2, 0), (2, 2), (0, 2)), 1)},
        member={str(i): dict(ni=i, nj=i % 4 + 1, e=1) for i in range(1, 5)},
        element={"1": {"1": dict(E=1000, G=400, A=1, Iy=1, Iz=1, J=1)}},
        inf_panel={"7": dict(nodes=["1", "2", "3", "4"],
                             triangles=[[1, 2, 3], [1, 3, 4]])},
        line={"1": dict(position=[dict(x=0, y=0.5), dict(x=2, y=0.5)]),
              "2": dict(position=[dict(x=0, y=1.5), dict(x=2, y=1.5)])},
        load={"1": dict(inf_panel="7", load_inf=[record],
                        load_node=[dict(n=2, tz=-3)])},
    )


def square_definitions(panel, *, area=True, coefficients=(1, 0, 0, 0), reverse=(), offset=(0, 0, 0), scale=1.):
    from fem.spatial_loads import SpatialLoad, SpatialLoadDefinitions, SpatialLoadPath

    a, b, c, d = coefficients
    paths = [((0, 0, 0), (2, 0, 0)), ((0, 2, 0), (2, 2, 0))][:2 if area else 1]
    values = [(a, a+2*b), (a+2*c, a+2*b+2*c+4*d)][:len(paths)]
    records = []
    for i, points in enumerate(paths):
        points = [tuple(offset[j] + scale*p[j] for j in range(3)) for p in points]
        if i in reverse:
            points, values[i] = points[::-1], values[i][::-1]
        records.append(SpatialLoadPath(i+1, points))
    return SpatialLoadDefinitions((panel,), records, (SpatialLoad(9, panel.id, tuple(p.id for p in records), values),))


def solver_panel(*, shell=False, area=True, fixed=True):
    """Real beam/shell model for the internal Solver acceptance boundary."""
    from fem.file_io import _read_json_model
    from fem.model import FemModel

    data = legacy_panel(area=area)
    if shell:
        data.pop('member')
        data['shell'] = {'1': dict(nodes=[1, 2, 3, 4], e=1, t=.1)}
        data['inf_panel']['7'] = dict(nodes=[1, 2, 3, 4], elements=[1])
    model = FemModel()
    model.read_json_model(_read_json_model(data))
    if fixed:
        for node in model.mesh.nodes:
            model.add_restraint(node, True, True, True, True, True, True)
    model._set_element_coordinates()
    return model


def solve_spatial_internal(model):
    """Stage-3 numerical entry; deliberately does not bypass public preflight."""
    result = model.solver.solve(model.mesh, model.material, model.boundary, model.elements)
    model.results = result
    result['analysis_type'] = 'static'
    model._post_process_results()
    return result
