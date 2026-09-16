import numpy as np


def wire(value):
    if isinstance(value, dict):
        return {str(k): wire(v) for k, v in value.items()}
    if isinstance(value, (list, tuple, np.ndarray)):
        return [wire(v) for v in value]
    if isinstance(value, np.generic):
        return value.item()
    return value
