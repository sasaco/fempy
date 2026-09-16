"""Every registered structural sample load/reference case is independently visible."""

import pytest

from tests.support.sample_runner import run_sample_case
from tests.support.samples import sample_cases

pytestmark = pytest.mark.regression
CASES = list(sample_cases())


@pytest.mark.parametrize("data_path,case_id", [(p, c) for _, p, c in CASES], ids=[i for i, _, _ in CASES])
def test_saved_sample_case(data_path, case_id):
    run_sample_case(data_path, case_id)
