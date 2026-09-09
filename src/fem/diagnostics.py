"""Stable analysis diagnostics shared by Python and transport boundaries."""
from __future__ import annotations

from copy import deepcopy
from typing import Any

import numpy as np


class DiagnosticErrorMixin:
    """Attach a stable machine code without breaking ValueError/RuntimeError APIs."""

    error_code = "analysis_failure"
    error_category = "analysis"
    http_status = 500

    def __init__(self, message: str, **details: Any):
        super().__init__(message)
        self.details = deepcopy(details)


class InputValidationError(DiagnosticErrorMixin, ValueError):
    error_code = "invalid_input"
    error_category = "input"
    http_status = 400


class UnsupportedAnalysisError(DiagnosticErrorMixin, ValueError):
    error_code = "unsupported_analysis"
    error_category = "unsupported"
    http_status = 400


class StructuralMechanismError(DiagnosticErrorMixin, ValueError):
    error_code = "structural_mechanism"
    error_category = "structural"
    http_status = 422


class NumericalConditionError(DiagnosticErrorMixin, ValueError):
    error_code = "numerical_ill_conditioning"
    error_category = "numerical"
    http_status = 422


class ModalConvergenceError(DiagnosticErrorMixin, RuntimeError):
    error_code = "modal_nonconvergence"
    error_category = "convergence"
    http_status = 422


def diagnostic_payload(error: BaseException) -> tuple[dict[str, Any], int]:
    """Return the public error object and HTTP status for any exception.

    Known diagnostic exceptions retain their structured details. Generic input
    exceptions remain ``invalid_input`` for compatibility; unexpected internal
    exceptions do not expose implementation details.
    """
    code = getattr(error, "error_code", None)
    category = getattr(error, "error_category", None)
    status = getattr(error, "http_status", None)
    details = deepcopy(getattr(error, "details", {}))

    if code is None:
        if isinstance(error, np.linalg.LinAlgError):
            code, category, status = "analysis_failure", "internal", 500
        elif isinstance(error, (ValueError, KeyError, TypeError)):
            code, category, status = "invalid_input", "input", 400
        else:
            code, category, status = "analysis_failure", "internal", 500

    message = str(error) if status != 500 or code != "analysis_failure" else (
        "Analysis or result processing failed"
    )
    payload = {
        "error": message,
        "error_code": code,
        "error_category": category,
        "converged": False,
    }
    if details:
        payload["details"] = details
        # Keep the established nonlinear fields at the top level as well.
        for key in ("step", "load_factor"):
            if key in details:
                payload[key] = details[key]
    return payload, int(status)
