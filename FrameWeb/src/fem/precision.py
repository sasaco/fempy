"""Compensated products for stiff systems, including platforms with 64-bit longdouble."""
import math

import numpy as np


def product_error(a, b, product):
    if hasattr(math, 'fma'):
        return math.fma(float(a), float(b), -float(product))
    from decimal import Decimal, localcontext
    with localcontext() as context:
        context.prec = 80
        return float(Decimal.from_float(float(a))*Decimal.from_float(float(b))
                     - Decimal.from_float(float(product)))


def sparse_product(matrix, high, low):
    matrix = matrix.tocsr()
    result = np.empty(matrix.shape[0])
    for i in range(matrix.shape[0]):
        start, end = matrix.indptr[i:i+2]
        terms = []
        for j, value in zip(matrix.indices[start:end], matrix.data[start:end]):
            product = value*high[j]
            terms.extend((product, product_error(value, high[j], product), value*low[j]))
        result[i] = math.fsum(terms)
    return result


def add_correction(high, low, delta):
    delta = delta+low
    result = high+delta
    part = result-high
    return result, (high-(result-part))+(delta-part)
