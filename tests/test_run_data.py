# このファイルの目的:
# - data 配下の各構造要素ディレクトリ（bar, shell, bend）の JSON ファイルをテスト対象として収集し、
#   FemModel を用いて構造要素解析を実行し、
#   期待結果（result）とキー・数値（浮動小数点は pytest.approx で比較）を検証する

import os
import glob
import pytest
from run_sample import run_sample

here = os.path.dirname(__file__)

# bar要素のテストデータ
bar_data_dir = os.path.join(here, "data", "bar")
bar_json_files = sorted(glob.glob(os.path.join(bar_data_dir, "*.json")))


@pytest.mark.parametrize("data_path", bar_json_files)
def test_bar_elements(data_path):
    """
    data/bar ディレクトリ配下のバー要素テストデータを実行
    """
    run_sample(data_path)


# shell要素のテストデータ
shell_data_dir = os.path.join(here, "data", "shell")
shell_json_files = sorted(glob.glob(os.path.join(shell_data_dir, "*.json")))


@pytest.mark.parametrize("data_path", shell_json_files)
def test_shell_elements(data_path):
    """
    data/shell ディレクトリ配下のシェル要素テストデータを実行
    """
    run_sample(data_path)


# bend要素のテストデータ
bend_data_dir = os.path.join(here, "data", "bend")
bend_json_files = sorted(glob.glob(os.path.join(bend_data_dir, "*.json")))


@pytest.mark.parametrize("data_path", bend_json_files)
def test_bend_elements(data_path):
    """
    data/bend ディレクトリ配下のベンド要素テストデータを実行
    """
    run_sample(data_path)


# 材料非線形要素のテストデータ
snap_data_dir = os.path.join(here, "data", "snap")
snap_json_files = sorted(glob.glob(os.path.join(snap_data_dir, "*.json")))


@pytest.mark.parametrize("data_path", snap_json_files)
def test_snap_elements(data_path):
    """
    data/snap ディレクトリ配下の材料非線形要素テストデータを実行
    """
    run_sample(data_path)
