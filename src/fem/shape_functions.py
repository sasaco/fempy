"""Natural-coordinate bases shared by shell elements and spatial loading."""
import numpy as np


def t3_shape(natural):
    r, s = natural
    return np.array([1 - r - s, r, s])


def t3_derivatives(natural):
    return np.array([[-1., -1.], [1., 0.], [0., 1.]])


def q4_shape(natural):
    """Bilinear basis in node order (-,-), (+,-), (+,+), (-,+)."""
    r, s = natural
    return .25 * np.array([(1-r)*(1-s), (1+r)*(1-s), (1+r)*(1+s), (1-r)*(1+s)])


def q4_derivatives(natural):
    r, s = natural
    return .25 * np.array([[-(1-s), -(1-r)], [1-s, -(1+r)],
                           [1+s, 1+r], [-(1+s), 1-r]])
