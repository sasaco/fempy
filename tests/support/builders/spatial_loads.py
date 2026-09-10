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
