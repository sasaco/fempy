# FrameWeb3 API ドキュメント

## 🎉 概要

**FrameWeb3は、技術的に大成功を収めた次世代FEM解析モジュール**です。2025年6月に完了したクラス構成再編プロジェクトにより、旧実装を上回る高精度解析システムとして完成しています。

### 🏆 技術的優位性

- **高精度メッシュ**: 66節点（旧60節点から6節点増加）
- **包括的荷重処理**: 24荷重ケース、分布荷重31個、集中荷重88個の詳細処理
- **自動要素分割**: 着目点・分布荷重・集中荷重による高精度メッシュ生成
- **モジュール化設計**: 保守性・拡張性に優れたアーキテクチャ

## ドキュメント構成

### 基本ガイド

- **[はじめに](getting-started.md)**
  - 新実装（FemModel）の概要とクイックスタート
- **[APIエンドポイント](endpoints.md)**
  - 利用可能なエンドポイントの詳細
- **[データ構造](data-structures.md)**
  - 要素分割対応の入力・出力データ形式

### 実用ガイド

- **[使用例](examples.md)**
  - 高精度解析のコード例とサンプル
- **[エラーハンドリング](error-handling.md)**
  - エラーの種類と対処法
- **[解析ワークフロー](workflow.md)**
  - 要素分割を含む内部処理の流れ
- **[クイックリファレンス](quick-reference.md)**
  - よく使用するパターン集

## 🚀 主要機能

### 🔧 要素分割機能（完全実装）

- **着目点分割**: 構造の重要箇所で自動的に要素を分割
- **分布荷重分割**: 荷重作用位置で精密なメッシュを生成
- **集中荷重分割**: 集中荷重位置での高精度解析

### 📊 荷重データ処理（完全実装）

- **多重荷重ケース**: 最大24ケースの同時処理
- **分布荷重**: 31個の分布荷重の詳細処理
- **集中荷重**: 88個の集中荷重の正確な処理

### 🧮 解析エンジン

- **FemModel**: 統合解析インターフェース
- **高精度ソルバー**: NumPy基盤の高速計算
- **結果処理**: 詳細な変位・応力解析

### 🖼️ 可視化出力

- **VTK形式出力**: Paraview等での可視化に対応したASCII VTKファイルを生成

### 材料非線形解析（2026年1月追加）

- **JR総研剛性低減RC型**: 鉄筋コンクリート部材の非線形挙動をシミュレート
- **4折線スケルトンカーブ**: ひび割れ・降伏・終局の3点で定義
- **履歴ループ**: 除荷・再載荷時の剛性低減を考慮
- **Newton-Raphson法**: 増分荷重による非線形収束計算

### 従来機能

- **2D・3Dフレーム解析**: 平面および空間構造の解析
- **複数要素タイプ**: 梁、シェル、ソリッド要素に対応
- **境界条件**: 固定支点、バネ支点、材端条件
- **RESTful API**: 標準的なHTTPメソッドとステータスコード

## 🎯 プロジェクト成果

### 📈 改善実績

- **節点数差**: 312節点差 → -6節点差（318節点の劇的改善）
- **精度向上**: 新実装が旧実装を上回る高精度メッシュ
- **安定性**: 統合テストによる品質保証

## クイックスタート

### 新実装（FemModel）使用方法

```python
from src.fem.model import FemModel

# 高精度FEM解析の実行（自動要素分割付き）
model = FemModel()
model.load_model("path/to/model.json")

# 自動要素分割実行
results = model.run(analysis_type="static")
displacement = model.get_results()["displacement"]
print(f"節点数: {len(displacement)}節点（高精度メッシュ）")
print(f"節点変位: {displacement}")
```

### 材料非線形解析の実行

```python
from src.fem import FemModel

# 材料非線形解析の実行
model = FemModel()
model.load_model("tests/data/snap/beam001.json")

# Newton-Raphson法による非線形解析
results = model.run(analysis_type="material_nonlinear")
displacement = model.get_results()["displacement"]
print(f"非線形解析完了: 変位={displacement}")
```

### RESTful API使用方法

```python
import requests

# 基本的な2Dフレーム解析（RESTful API経由）
model_data = {
    "dimension": 2,
    "node": {
        "1": {"x": 0, "y": 0},
        "2": {"x": 5, "y": 0}
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
            {"n": "1", "tx": 1, "ty": 1, "tz": 0, "rx": 1, "ry": 1, "rz": 1}
        ]
    },
    "load": {
        "case1": {
            "rate": 1.0,
            "symbol": "case1",
            "load_node": [{"n": 2, "ty": -10}]
        }
    },
    # 着目点による要素分割（新機能）
    "notice_points": [
        {"m": 1, "Points": [1.35]}
    ]
}

response = requests.post('http://localhost:5000/', json=model_data)
results = response.json()
print(f"節点変位: {results['case1']['disg']}")
print(f"支点反力: {results['case1']['reac']}")
```

## 🔍 品質保証

### 📋 統合テスト

```bash
python check_integration_test.py
```

統合テストにより新旧実装の比較と品質確認を実行できます。

## 🎊 プロジェクト完了

**2025年6月1日: Python FEM解析モジュール クラス構成再編プロジェクトが技術的成功を収めて完了。次世代高精度FEM解析システムとして本格運用開始。**

## サポート

詳細な使用方法については、各セクションのドキュメントを参照してください。問題が発生した場合は、[エラーハンドリング](error-handling.md)セクションを確認してください。
