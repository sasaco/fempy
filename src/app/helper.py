def isInt(obj):
    """引数が整数に変換可能か判定する

    Args:
        obj (any): 判定対象

    Returns:
        _ (bool): 判定結果True or False
    """
    if type(obj) is int:
        return True
    try:
        int(obj, 10)
    except Exception:
        return False
    else:
        return True
    

def isFloat(obj):
    """引数が浮動小数点数に変換可能か判定する

    Args:
        obj (any): 判定対象

    Returns:
        _ (bool): 判定結果True or False
    """
    if type(obj) is float:
        return True
    try:
        float(obj)
    except Exception:
        return False
    else:
        return True
    

def convInt(obj) -> int:
    """isIntがTrueとなる引数を整数にして返す

    Args:
        obj (any): 文字列または数値

    Returns:
        _ (int): 整数
    """
    if type(obj) is int:
        return obj
    return int(obj, 10)