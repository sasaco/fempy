"""Legacy shell material selection and documented A-as-thickness input."""


def data():
    return dict(
        node={"1": dict(x=0, y=0, z=0), "2": dict(x=1, y=0, z=0), "3": dict(x=0, y=1, z=0)},
        shell={"7": dict(nodes=[1, 2, 3], e=1)},
        element={"1": {"1": dict(E=1000, nu=0.25, A=0.2)}, "2": {"1": dict(E=1000, nu=0.25, A=0.3)}},
        load={"1": dict(element=2)},
    )
