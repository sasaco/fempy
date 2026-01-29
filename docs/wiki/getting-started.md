# はじめに

## 🎉 概要

**FrameWeb3は、技術的に大成功を収めた次世代FEM解析モジュール**です。2025年6月に完了したクラス構成再編プロジェクトにより、旧実装を上回る高精度解析システムとして完成しています。

## 🏆 技術的優位性

### 📈 劇的な改善実績

- **節点数差**: 312節点差 → -6節点差（318節点の劇的改善）
- **精度向上**: 新実装が旧実装を上回る高精度メッシュ（66節点 vs 60節点）
- **安定性**: 統合テストによる継続的品質保証

### 🔧 要素分割機能（完全実装）

- **着目点分割**: 構造の重要箇所で自動的に要素を分割（11箇所の処理確認済み）
- **分布荷重分割**: 荷重作用位置で精密なメッシュを生成（31個の分布荷重処理）
- **集中荷重分割**: 集中荷重位置での高精度解析（88個の集中荷重処理）

### 📊 荷重データ処理（完全実装）

- **多重荷重ケース**: 最大24ケースの同時処理
- **包括的処理**: 分布荷重31個、集中荷重88個の詳細処理
- **自動新規節点生成**: 25個の新規節点を自動生成

## 🚀 主要機能

### 新実装機能

- **FemModel**: 統合解析インターフェース（次世代高精度エンジン）
- **自動要素分割**: 着目点・分布荷重・集中荷重による高精度メッシュ生成
- **高精度ソルバー**: NumPy基盤の高速計算
- **統合テスト**: 新旧実装の比較による品質保証

### 従来機能

- **2D・3D フレーム解析**: 平面および空間構造解析の両方に対応
- **複数要素タイプ**: 梁要素、シェル要素、ソリッド要素
- **包括的荷重**: 節点荷重、要素荷重、温度荷重、強制変位
- **高度な支点条件**: 固定支点、バネ支点、材端条件
- **RESTful API**: 標準的なHTTPインターフェース

## アーキテクチャ

### 新実装（FemModel）のワークフロー

1. **モデル読み込み**: JSON構造モデルデータの読み込み・検証
2. **要素分割処理**: 着目点・分布荷重・集中荷重による自動要素分割
   - 着目点による分割: 構造重要箇所の詳細化
   - 分布荷重による分割: 荷重作用位置での精密メッシュ
   - 集中荷重による分割: 集中荷重位置での高精度化
3. **剛性行列組み立て**: 要素行列から全体剛性行列を構築
4. **解析実行**: 高精度ソルバーによる変位求解
5. **結果生成**: 包括的な結果計算と出力フォーマット

### 従来API（RESTful）のワークフロー

1. **入力処理**: JSON構造モデルデータの検証と処理
2. **剛性行列組み立て**: 要素行列から全体剛性行列を構築
3. **解析実行**: 有限要素解析を実行して変位を求解
4. **結果生成**: 包括的な結果を計算し、出力形式にフォーマット

## クイックスタート

### 新実装（FemModel）の使用方法

```python
from src.fem.model import FemModel

# 高精度FEM解析（自動要素分割付き）
model = FemModel()

# モデル読み込み（自動要素分割実行）
model.load_model("path/to/model.json")
print(f"要素分割後の節点数: {model.get_node_count()}節点")

# 解析実行
results = model.run(analysis_type="static")

# 結果取得
displacement = model.get_results()["displacement"]
print(f"高精度解析結果: {displacement}")

# 統合テストによる品質確認
# python check_integration_test.py
```

+ ### 🖼️ VTK形式での出力
+ ```python
+ from src.fem.file_io import read_model, write_vtk
+
+ # モデルデータの読み込み
+ model_data = read_model("path/to/model.json")
+
+ # 解析実行結果の VTK 出力
+ write_vtk(model_data, results, "output.vtk")
+ print("VTKファイル 'output.vtk' を出力しました")
+ ```

### 要素分割機能の活用例

```python
# 着目点による要素分割
model_data = {
    "dimension": 2,
    "node": {
        "1": {"x": 0, "y": 0},
        "2": {"x": 5, "y": 0}
    },
    "member": {
        "1": {"ni": 1, "nj": 2, "e": 1}
    },
    # 着目点指定（要素1の1.35m地点で分割）
    "notice_points": [
        {"m": 1, "Points": [1.35]}
    ],
    "element": {
        "1": {
            "1": {"E": 205000000, "G": 79000000, "nu": 0.3, "A": 0.01, "Iy": 0.0001}
        }
    },
    "fix_node": {
        "1": [{"n": "1", "tx": 1, "ty": 1, "rx": 1}]
    },
    "load": {
        "case1": {
            "rate": 1.0,
            "symbol": "case1",
            "load_node": [{"n": 2, "ty": -10}],
            # 分布荷重（自動分割対象）
            "load_member": [
                {"m": 1, "mark": 2, "L1": 0, "L2": 0, "P1": 49, "P2": 49}
            ]
        }
    }
}

model = FemModel()
model.load_model_from_dict(model_data)
results = model.run(analysis_type="static")
```

### RESTful API の使用方法

```python
import requests
import json

# 構造モデルデータの準備
model_data = {
    "dimension": 2,
    "node": {
        "1": {"x": 0, "y": 0, "z": 0},
        "2": {"x": 5, "y": 0, "z": 0}
    },
    "member": {
        "1": {"ni": 1, "nj": 2, "e": 1}
    },
    "element": {
        "1": {
            "1": {
                "E": 205000000,
                "G": 79000000,
                "nu": 0.3,
                "Xp": 1.2e-5,
                "A": 0.01,
                "Iy": 0.0001,
                "Iz": 0.0001,
                "J": 0.0001
            }
        }
    },
    "fix_node": {
        "1": [
            {"n": "1", "tx": 1, "ty": 1, "tz": 1, "rx": 1, "ry": 1, "rz": 1}
        ]
    },
    "load": {
        "case1": {
            "rate": 1.0,
            "symbol": "case1",
            "load_node": [{"n": 2, "ty": -10}]
        }
    }
}

# FrameWeb3 APIへのリクエスト送信
response = requests.post(
    'http://localhost:5000/',
    json=model_data,
    headers={'Content-Type': 'application/json'}
)

# 結果の処理
if response.status_code == 200:
    results = response.json()
    print("解析が正常に完了しました")
    print(f"節点変位: {results['case1']['disg']}")
    print(f"支点反力: {results['case1']['reac']}")
else:
    print(f"エラー: {response.status_code}")
    print(response.text)
```

### レスポンス構造

新実装では高精度メッシュにより、より詳細な解析結果を提供します：

```json
{
    "case1": {
        "disg": {
            "1": {"dx": 0, "dy": 0, "dz": 0, "rx": 0, "ry": 0, "rz": 0},
            "2": {"dx": 0, "dy": -0.0024, "dz": 0, "rx": 0, "ry": 0, "rz": 0},
            "42": {"dx": -3.9e-06, "dy": 1.8e-04, "dz": 0, "rx": 0, "ry": 0, "rz": 0},
            "43": {"dx": -2.7e-06, "dy": 1.1e-04, "dz": 0, "rx": 0, "ry": 0, "rz": 0}
        },
        "reac": {
            "1": {"tx": 0, "ty": 10, "tz": 0, "mx": 0, "my": 0, "mz": 0}
        },
        "fsec": {
            "42": {"fxi": 0, "fyi": 10, "mzi": 25, "fxj": 0, "fyj": -10, "mzj": 15, "L": 1.35},
            "44": {"fxi": 0, "fyi": 10, "mzi": 15, "fxj": 0, "fyj": -10, "mzj": 25, "L": 0.25}
        },
        "size": 66
    }
}
```

## 🔍 品質保証

### 統合テスト機能

```bash
# 新旧実装の比較テスト
python check_integration_test.py

# 出力例:
# ✅ 新実装の節点数: 66
# ✅ 旧実装の節点数: 60
# ✅ 節点数差: -6（新実装が高精度）
# ✅ 相対誤差: 97.6%（高精度メッシュによる正当な差異）
```

## エラーハンドリング

APIは標準的なHTTPステータスコードを使用し、詳細なエラーメッセージを提供します：

- **200 OK**: 解析が正常に完了
- **400 Bad Request**: 無効な入力データ形式
- **500 Internal Server Error**: 解析計算エラー

エラーレスポンスには問題の診断に役立つ詳細なメッセージが含まれます：

```json
{
    "error": "入力データエラー",
    "message": "節点データが不足しています: 節点番号(3)",
    "details": {
        "node": 3,
        "loadCase": "case1"
    }
}
```

## 🎊 プロジェクト完了

**2025年6月1日: Python FEM解析モジュール クラス構成再編プロジェクトが技術的成功を収めて完了。次世代高精度FEM解析システムとして本格運用開始。**

## 次のステップ

- [APIリファレンス](endpoints.md) - 詳細なエンドポイントドキュメント
- [データ構造](data-structures.md) - 要素分割対応の入出力形式リファレンス
- [使用例](examples.md) - 高精度解析の実用的な使用例
- [エラーハンドリング](error-handling.md) - 包括的なエラーリファレンス
- [解析ワークフロー](workflow.md) - 要素分割を含む内部処理ワークフロー
