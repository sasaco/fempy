"""Physical identities and cross-implementation high-precision solid references."""
from decimal import Decimal as D, localcontext
import copy
import json

import numpy as np
import pytest

from tests.elements.test_phase4_quadratic_solids import CASES
from v0_decimal_reference import stiffness, inverse
from v0_refined_reference import solve_source
from restore_phase4_sources import ROOT, read_v0
from restore_phase4_supports import completed_reference


@pytest.mark.parametrize('kind,coords,volume', CASES)
def test_original_shape_stiffness_has_exact_affine_work_and_rigid_modes(kind,coords,volume):
    source_kind={'tetra2':'TetraElement2','wedge2':'WedgeElement2','hexa2':'HexaElement2'}[kind]
    k=stiffness(source_kind,tuple(tuple(float(v) for v in r) for r in coords),1000.,.25)
    with localcontext() as ctx:
        ctx.prec=50
        # exx=1 gives sxx=1200 and energy=600*V, independently of interpolation.
        u=[D(str(v)) for row in coords for v in (row[0],0,0)]
        energy=sum(u[i]*sum(a*b for a,b in zip(row,u)) for i,row in enumerate(k))/2
        exact_volume={'tetra2':D(1)/6,'wedge2':D(1),'hexa2':D(8)}[kind]
        assert abs(energy-600*exact_volume) < D('1e-35')
        for axis in np.eye(3):
            for field in (np.tile(axis,(len(coords),1)),np.cross(axis,coords)):
                rigid=[D(str(float(v))) for v in field.ravel()]
                assert max(abs(sum(a*b for a,b in zip(row,rigid))) for row in k) < D('1e-35')


def test_decimal_reference_inverse_on_skew_jacobian():
    with localcontext() as ctx:
        ctx.prec=50
        j=[[D(v) for v in r] for r in [[2,1,3],[1,4,2],[0,2,5]]]
        inv,det=inverse(j)
        assert det == 33
        for i in range(3):
            for c in range(3):
                assert abs(sum(j[i][a]*inv[a][c] for a in range(3))-int(i==c)) < D('1e-45')


@pytest.mark.parametrize('stem',['sampleBendHexa2','sampleBendWedge2','sampleBendTetra2'])
def test_quadratic_solid_all_outputs_against_independent_decimal_source(stem):
    from run_sample import FemModel, _read_json_model, compare_legacy_result
    path=ROOT/'tests/data/bend'/(stem+'.json'); before=path.read_bytes();data=json.loads(before)
    source_path=ROOT/'docs/v0/testdata/bend'/(stem+'.out')
    reference=solve_source(source_path,decimal_stiffness=True)
    fixed=completed_reference(data,read_v0(source_path),reference)
    model=FemModel();model.read_json_model(_read_json_model(copy.deepcopy(data)))
    compare_legacy_result(model.run(),fixed['result']['1'],model,data)
    assert path.read_bytes() == before
