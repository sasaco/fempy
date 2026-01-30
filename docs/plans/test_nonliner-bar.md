# 材料非線形解析結果をbeam001.jsonに書き出す計画

## 概要
`beam001.json`の非線形材料を使った解析を実行し、`2D_Sample01.json`と同形式で`result`フィールドに結果を書き出す。

## 現状分析

### beam001.jsonの構造
- 節点: 4ノード（1, 2, 3, 4）
- 部材: 3部材（1, 2, 3）
- 材料: ID=2に`jr_stiffness_reduction`型の非線形材料が定義済み
- 荷重: ノード1にX方向1000の集中荷重
- 拘束: ノード4に6自由度固定

### 期待される結果形式（2D_Sample01.json準拠）
```json
{
  "result": {
    "1": {
      "disg": { "1": {"dx", "dy", "dz", "rx", "ry", "rz"}, ... },
      "reac": { "4": {"tx", "ty", "tz", "mx", "my", "mz"} },
      "fsec": {
        "1": {
          "P1": {
            "fxi", "fyi", "fzi", "mxi", "myi", "mzi",
            "fxj", "fyj", "fzj", "mxj", "myj", "mzj", "L"
          }
        }
      },
      "shell_fsec": {},
      "shell_results": {},
      "size": 24
    }
  }
}
```

## 実装計画

### Step 1: 結果生成スクリプトの作成
`tests/write_snap_result.py`を作成し、以下を実装:

1. **JSONファイル読み込み**
   - `beam001.json`を読み込む

2. **非線形解析実行**
   - `FemModel.load_model()`でモデル読み込み
   - `FemModel.run('material_nonlinear')`で解析実行

3. **結果フォーマット変換**
   NonlinearSolverの出力を2D_Sample01.json形式に変換:
   - `node_displacements` → `disg`
   - 反力計算 → `reac`
   - 断面力計算 → `fsec`
   - `shell_fsec`, `shell_results`は空辞書
   - `size` = 節点数 × 6

4. **結果書き込み**
   - 元のJSONに`result`フィールドを追加
   - ファイルに保存

### Step 2: 必要な追加機能

#### 反力（reac）計算
拘束節点の反力 = 外力 - 内力（Solver.solveで計算済み形式を参考）

#### 断面力（fsec）計算
各要素について:
- 要素変位から要素内力を計算
- 要素座標系での断面力に変換
- `fxi, fyi, fzi, mxi, myi, mzi, fxj, fyj, fzj, mxj, myj, mzj, L`形式で出力

### Step 3: 実行とテスト
1. スクリプト実行して`beam001.json`に結果書き出し
2. `run_sample.py`で結果の妥当性確認
3. pytest実行でテスト通過確認

## 修正対象ファイル
| ファイル | 変更内容 |
|----------|----------|
| `tests/write_snap_result.py` | 新規作成 - 結果生成スクリプト |
| `tests/data/snap/beam001.json` | result フィールド追加 |

## 検証方法
1. 生成された`result`が2D_Sample01.jsonと同形式であること確認
2. `python -m pytest tests/test_run_data.py::test_snap_elements` 実行
