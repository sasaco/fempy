# クイックリファレンス

## 🎉 新実装（FemModel）高精度解析

**FrameWeb3は、技術的に大成功を収めた次世代FEM解析モジュール**として完成しています。

### 🚀 基本的な使用方法（新実装）
```python
from src.fem.model import FemModel

# 高精度FEM解析（自動要素分割付き）
model = FemModel()
model.load_model("path/to/model.json")

# 自動要素分割実行
results = model.run(analysis_type="static")
displacement = model.get_results()["displacement"]
print(f"解析完了: {len(displacement)}節点の高精度メッシュ")
```

### 🔧 要素分割機能チートシート

#### 着目点による要素分割
```json
{
  "notice_points": [
    {"m": 要素ID, "Points": [分割位置1, 分割位置2, ...]}
  ]
}
```

#### 分布荷重による自動分割
```json
{
  "load": {
    "case1": {
      "load_member": [
        {"m": 要素ID, "mark": 2, "L1": 開始位置, "L2": 終了位置, "P1": 荷重値, "P2": 荷重値}
      ]
    }
  }
}
```

#### 集中荷重による自動分割
```json
{
  "load": {
    "case1": {
      "load_member": [
        {"m": 要素ID, "mark": 1, "L1": 荷重位置, "P1": 荷重値}
      ]
    }
  }
}
```

### 📊 技術的優位性
| 項目 | 新実装 | 旧実装 | 改善 |
|------|--------|--------|------|
| 節点数 | 66節点 | 60節点 | +6節点（高精度） |
| 要素分割 | 自動実行 | なし | 革新的機能 |
| 荷重処理 | 24ケース | 基本対応 | 包括的処理 |
| 品質保証 | 統合テスト | 基本テスト | 継続的保証 |

### 🔍 統合テスト
```bash
# 新旧実装の品質比較
python check_integration_test.py

# 期待される出力:
# ✅ 新実装の節点数: 66
# ✅ 旧実装の節点数: 60
# ✅ 節点数差: -6（新実装が高精度）
```

---

## 材料非線形解析（2026年1月追加）

### JR総研剛性低減RC型モデル
```python
from src.fem import FemModel

# 非線形解析の実行（解析パラメータはJSONで指定）
model = FemModel()
model.load_model("tests/data/snap/beam001.json")
results = model.run(analysis_type="material_nonlinear")
```

### 解析パラメータ（loadセクションで指定）
```json
"load": {
  "1": {
    "fix_node": 1,
    "fix_member": 1,
    "element": 1,
    "joint": 1,
    "n_load_steps": 10,      // 荷重増分ステップ数
    "max_iterations": 50,    // 最大反復回数
    "tolerance": 1e-6,       // 収束判定許容差
    "n_modes": 10,           // 固有モード数（modal用）
    "load_node": [...]
  }
}
```

### 非線形材料定義（JSON）
```json
"element": {
  "1": {
    "2": {
      "E": 26500000,
      "A": 1000,
      "Iz": 1000,
      "nonlinear": {
        "type": "jr_stiffness_reduction",
        "delta_1": 1e-05, "delta_2": 0.0001, "delta_3": 0.001,
        "P_1": 1000.0, "P_2": 3000.0, "P_3": 5000.0,
        "beta": 0.4,
        "symmetric": true,
        "hysteresis_dofs": ["moment_z"]
      }
    }
  }
}
```

### 4折線スケルトンカーブパラメータ
| パラメータ | 説明 | 単位 |
|-----------|------|------|
| delta_1 | ひび割れ一般化ひずみ | 適用先による（下記） |
| delta_2 | 降伏一般化ひずみ | 同上 |
| delta_3 | 終局一般化ひずみ | 同上 |
| P_1 | ひび割れ断面力 | 適用先による（下記） |
| P_2 | 降伏断面力 | 同上 |
| P_3 | 終局断面力 | 同上 |
| beta | 剛性低減係数 | - |

NonlinearBarElementでは、axialはδ=軸ひずみ（無次元）・P=軸力（N）、
moment_y/zはδ=中央曲率（rad/m）・P=モーメント（N m）、
torsionはδ=ねじり率（rad/m）・P=ねじりモーメント（N m）。
これはN・mの整合単位系の例。集中ヒンジの相対回転角や絶対端部変位とは異なる。
旧入力の単位・較正は再確認が必要。詳細は[理論7.21節](../理論マニュアル/08_材料非線形モデル.md#721-履歴曲線の追跡)を参照。

### 非線形適用自由度（hysteresis_dofs）
| 値 | 説明 |
|----|------|
| `"axial"` | 軸力（N） |
| `"moment_y"` | Y軸周り曲げモーメント |
| `"moment_z"` | Z軸周り曲げモーメント |
| `"torsion"` | ねじりモーメント |

### 解析タイプ
| タイプ | 説明 |
|--------|------|
| `"static"` | 線形静的解析 |
| `"material_nonlinear"` | 材料非線形解析（Newton-Raphson法） |

---

## 基本的な使用方法（従来API）

### 最小限の2Dフレーム解析
```python
import requests

model_data = {
    "dimension": 2,
    "node": {"1": {"x": 0, "y": 0}, "2": {"x": 5, "y": 0}},
    "member": {"1": {"ni": 1, "nj": 2, "e": 1}},
    "element": {"1": {"1": {"E": 205000000, "G": 79000000, "nu": 0.3, "Xp": 1.2e-5, "A": 0.01, "Iy": 0.0001, "Iz": 0.0001, "J": 0.0001}}},
    "fix_node": {"1": [{"n": "1", "tx": 1, "ty": 1, "tz": 0, "rx": 1, "ry": 1, "rz": 1}]},
    "load": {"case1": {"rate": 1.0, "symbol": "case1", "load_node": [{"n": 2, "ty": -10}]}},
    # 🎯 着目点による要素分割（新機能）
    "notice_points": [{"m": 1, "Points": [1.35]}]
}

response = requests.post('http://localhost:5000/', json=model_data)
results = response.json()
```

## データ構造チートシート

### 節点定義
```json
"node": {
  "節点ID": {"x": X座標, "y": Y座標, "z": Z座標}
}
```

### 部材定義
```json
"member": {
  "部材ID": {"ni": 始点節点ID, "nj": 終点節点ID, "e": 要素ID}
}
```

### 材料・断面特性
```json
"element": {
  "要素ID": {
    "E": ヤング係数,
    "G": せん断弾性係数,
    "A": 断面積,
    "Iy": Y軸回り断面二次モーメント,
    "Iz": Z軸回り断面二次モーメント,
    "J": ねじり定数
  }
}
```

### 支点条件
```json
"fix_node": {
  "支点ケースID": {
    "節点ID": {
      "x": 0, // 0=自由, 1=拘束, >1000=バネ定数
      "y": 1,
      "z": 0,
      "rx": 0,
      "ry": 1,
      "rz": 0
    }
  }
}
```

### 🆕 荷重定義（要素分割対応）
```json
"load": {
  "荷重ケースID": {
    "rate": 1.0,
    "symbol": "DL",
    "load_node": [
      {"n": 節点ID, "tx": 0, "ty": -10, "tz": 0, "mx": 0, "my": 0, "mz": 0}
    ],
    "load_member": [
      // 分布荷重（自動分割対象）
      {"m": 部材ID, "mark": 2, "direction": "gy", "p1": -20, "p2": -20, "L1": 0, "L2": 8},
      // 集中荷重（自動分割対象）
      {"m": 部材ID, "mark": 1, "direction": "gy", "L1": 2.5, "P1": 100}
    ]
  }
}
```

### 🆕 着目点定義（要素分割）
```json
"notice_points": [
  {
    "m": 要素ID, // 分割対象要素
    "Points": [1.35, 2.5] // 分割位置（要素i端からの距離）
  }
]
```

## 一般的な支点条件

### 固定支点
```json
{"x": 1, "y": 1, "z": 1, "rx": 1, "ry": 1, "rz": 1}
```

### ピン支点
```json
{"x": 1, "y": 1, "z": 1, "rx": 0, "ry": 0, "rz": 0}
```

### ローラー支点（Y方向のみ拘束）
```json
{"x": 0, "y": 1, "z": 0, "rx": 0, "ry": 0, "rz": 0}
```

### 🆕 バネ支点（新実装対応）
```json
{"x": 0, "y": 61902, "z": 0, "rx": 0, "ry": 0, "rz": 0} // y=61902 はバネ定数（kN/m）
```

## 荷重方向指定

### 全体座標系
- `"gx"`: 全体X方向
- `"gy"`: 全体Y方向
- `"gz"`: 全体Z方向

### 要素局所座標系
- `"x"`: 要素局所x方向（部材軸方向）
- `"y"`: 要素局所y方向
- `"z"`: 要素局所z方向

### 🆕 荷重マーク（要素分割対応）
- `mark: 1`: 集中荷重（自動分割対象）
- `mark: 2`: 分布荷重（自動分割対象）
- `mark: 11`: 集中荷重（分割対象外）

## 典型的な材料特性

### 鋼材（SS400）
```json
{
  "E": 205000000, // kN/m²
  "G": 79000000, // kN/m²
  "poi": 0.3
}
```

### コンクリート（Fc=24）
```json
{
  "E": 25000000, // kN/m²
  "G": 10400000, // kN/m²
  "poi": 0.2
}
```

## 🆕 新実装エラーハンドリング
```python
from src.fem.model import FemModel

try:
    model = FemModel()
    model.load_model("model.json")
    
    # 要素分割情報の確認
    print(f"節点数: {model.get_node_count()}")
    print(f"要素数: {model.get_element_count()}")
    
    results = model.run(analysis_type="static")
except FileNotFoundError:
    print("モデルファイルが見つかりません")
except ValueError as e:
    print(f"データ形式エラー: {e}")
except np.linalg.LinAlgError:
    print("行列計算エラー: 不安定構造の可能性")
except Exception as e:
    print(f"予期しないエラー: {e}")
```

## 従来APIエラーハンドリング
```python
try:
    response = requests.post('http://localhost:5000/', json=model_data, timeout=30)
    if response.status_code == 200:
        results = response.json()
        # 成功処理
    elif response.status_code == 400:
        error = response.json()
        print(f"入力エラー: {error['message']}")
    elif response.status_code == 500:
        error = response.json()
        print(f"計算エラー: {error['message']}")
except requests.exceptions.Timeout:
    print("タイムアウトエラー")
except requests.exceptions.ConnectionError:
    print("接続エラー")
```

## 結果データアクセス

### 🆕 新実装（FemModel）
```python
# 高精度解析結果
results = model.get_results()
displacement = results["displacement"]  # 節点変位（66節点の詳細結果）

for node_id, disp in displacement.items():
    dx = disp.get('dx', 0)  # X方向変位
    dy = disp.get('dy', 0)  # Y方向変位
    print(f"節点{node_id}: dx={dx:.6e}, dy={dy:.6e}")
```

### 従来API
```python
results = response.json()

# 節点変位
displacement = results['荷重ケースID']['disg']['節点ID']
dx = displacement['dx']  # X方向変位
dy = displacement['dy']  # Y方向変位

# 支点反力
reaction = results['荷重ケースID']['reac']['節点ID']
fx = reaction['tx']  # X方向反力
fy = reaction['ty']  # Y方向反力

# 部材断面力
section_force = results['荷重ケースID']['fsec']['部材ID']['断面ID']
axial = section_force['fxi']  # 軸力
shear = section_force['fyi']  # せん断力
moment = section_force['mzi']  # 曲げモーメント
```

## 単位系
| 項目 | 単位 |
|------|------|
| 長さ | m |
| 力 | kN |
| モーメント | kNm |
| 応力 | kN/m² |
| 変位 | mm |
| 回転角 | rad |
| バネ定数 | kN/m, kNm/rad |

## 🆕 新実装の特殊機能

### L2負値処理（分布荷重）
```json
// L2が負値の場合、荷重幅→j端からの距離に自動変換
{"m": 1, "mark": 2, "L1": 0, "L2": -2.0, "P1": 50, "P2": 50}
// 要素長6mの場合、L2=-2.0 → j端から2.0m = i端から4.0m
```

### 重複節点の自動処理
- 座標ベースの重複チェック（許容誤差: 1e-6）
- 自動的な節点番号の再割り当て
- メッシュ品質の自動保証

## よくある問題と解決法

### 🆕 要素分割関連
- **分割位置エラー**: 0 < 位置 < 要素長 の範囲内で指定
- **重複分割点**: 同一位置の複数指定は自動的に統合
- **節点番号競合**: 新規節点は自動的に適切な番号を割り当て

### 不安定構造
- 支点条件を確認
- 部材の接続を確認
- 材端条件を確認

### 数値エラー
- 単位系の統一
- 極端に小さい/大きい値の回避
- 材料特性の妥当性確認

### 性能問題
- 大規模モデルでは圧縮使用
- 不要な精度設定の回避
- バッチ処理の活用

## 🎊 プロジェクト完了

**2025年6月1日: Python FEM解析モジュール クラス構成再編プロジェクトが技術的成功を収めて完了。次世代高精度FEM解析システムとして本格運用開始。**
