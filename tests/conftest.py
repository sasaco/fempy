import pytest
import sys
import os

# プロジェクトルートを sys.path に追加
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '../src'))) 
# srcディレクトリをPythonパスに追加
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))) 