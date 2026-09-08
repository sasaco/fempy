"""Large rigid displacement must not create quadratic-solid support forces."""
from decimal import Decimal
import numpy as np
import pytest

from tests.elements.test_phase4_quadratic_solids import CASES, make_model
from v0_decimal_reference import stiffness


@pytest.mark.parametrize('kind,coords,volume', CASES)
@pytest.mark.parametrize('mode', ['translation', 'rotation', 'strain'])
def test_quadratic_reactions_with_large_rigid_displacement(kind, coords, volume, mode):
    model, element = make_model(kind, coords)
    xyz=np.asarray(coords,dtype=float)
    if mode == 'rotation':
        displacement=np.cross([512.,0.,0.],xyz)
    else:
        displacement=np.full_like(xyz,512.)
        if mode == 'strain': displacement[:,0] += xyz[:,0]/1024
    for nid, values in zip(model.mesh.nodes,displacement):
        model.add_restraint(nid,True,True,True)
        model.add_forced_displacement(nid,dx=values[0],dy=values[1],dz=values[2])
    result=model.run()
    np.testing.assert_array_equal(result['displacement'].reshape(-1,3), displacement)
    if mode == 'strain':
        source_kind={'tetra2':'TetraElement2','wedge2':'WedgeElement2','hexa2':'HexaElement2'}[kind]
        k=stiffness(source_kind,tuple(tuple(float(v) for v in row) for row in coords),1000.,.25)
        # Only the strain part is needed; rigid translation does no work.
        u=[Decimal.from_float(float(v)) for v in (xyz*np.array([1/1024,0,0])).ravel()]
        expected=np.array([float(sum(a*b for a,b in zip(row,u))) for row in k]).reshape(-1,3)
    else: expected=np.zeros_like(xyz)
    actual=np.array([[result['reaction_forces'][n][key] for key in ('fx','fy','fz')] for n in model.mesh.nodes])
    np.testing.assert_allclose(actual,expected,rtol=1e-8,atol=1e-10)
