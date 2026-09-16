import numpy as np


def elastic_matrix(length, shear):
    """Closed-form Timoshenko blocks in right-handed local coordinates."""
    k = np.zeros((12, 12))
    for indices, rigidity in [([0, 6], 6000), ([3, 9], 560)]:
        k[np.ix_(indices, indices)] = rigidity / length * np.array([[1, -1], [-1, 1]])
    for indices, ei, ga, sign in [([1, 5, 7, 11], 1800, 1440, 1), ([2, 4, 8, 10], 800, 1920, -1)]:
        l = length
        phi = 12 * ei / (ga * l**2) if shear else 0
        block = (
            ei
            / (l**3 * (1 + phi))
            * np.array(
                [
                    [12, sign * 6 * l, -12, sign * 6 * l],
                    [sign * 6 * l, (4 + phi) * l * l, -sign * 6 * l, (2 - phi) * l * l],
                    [-12, -sign * 6 * l, 12, -sign * 6 * l],
                    [sign * 6 * l, (2 - phi) * l * l, -sign * 6 * l, (4 + phi) * l * l],
                ]
            )
        )
        k[np.ix_(indices, indices)] = block
    return k
