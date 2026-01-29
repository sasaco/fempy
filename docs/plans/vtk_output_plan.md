# VTK出力機能計画書

## 1. 背景と目的
- 現状: JSONやカスタムフォーマットで解析結果を出力している。
- 目的: Paraview等で可視化可能なVTK形式でも出力し、ユーザー利便性と解析結果の共有性を向上させる。

## 2. 出力形式選定
|形式|特徴|備考|
|:--|:--|:--|
|Legacy VTK (ASCII)|可読性が高く実装が簡易|まずはこちらを実装|
|XML VTK (.vtu, .vts)|構造化され拡張性あり|今後対応|

## 3. 主要要件
1. ノード座標・要素拓撲情報の出力
2. 解析結果（変位、応力、ひずみなど）のポイント／セルデータ出力
3. 単一ファイル（.vtk、ASCII）による出力
4. 出力パスとファイル名の指定オプション提供

## 4. 実装方針
### 4.1 モジュール構成
- 新規モジュール: `src/fem/vtk_writer.py`
  - クラス: `VTKWriter`
  - 主なメソッド:
    - `write_header()`
    - `write_points(nodes)`
    - `write_cells(elements)`
    - `write_point_data(data_dict)`
    - `write_cell_data(data_dict)`
    - `write_footer()`
- 既存モジュールへの統合:
  - `src/fem/file_io.py` にラッパー関数 `write_vtk(model, results, path)` を追加
  - `src/fem/result_processor.py` から呼び出し

### 4.2 データマッピング
- モデル (`src/fem/model.py`) からノード・要素情報をフェッチ
- `result_processor` が算出した応力・変位などを辞書形式で保持し、`VTKWriter` に渡す

### 4.3 ファイル生成ステップ
1. ヘッダ出力 (`# vtk DataFile Version 3.0`, 説明コメント等)
2. `DATASET UNSTRUCTURED_GRID`
3. `POINTS n float`
4. `CELLS m size`
5. `CELL_TYPES m`
6. `POINT_DATA n`, `CELL_DATA m`

## 5. テスト計画
- **ユニットテスト**: 小規模モデルを用いて出力ファイル内容を文字列比較
- **可視化確認**: Paraviewで正しくメッシュとデータが表示されることを確認
- **既存テストデータ適用**: JSONベースのサンプルに対するVTK出力結果を参照サンプルと突き合わせ

## 6. スケジュール（目安）
|工程|日数|
|:--|:--|
|要件定義・設計|1日|
|実装|2日|
|テスト・コードレビュー|1日|
|ドキュメント整備|0.5日|

## 7. 今後の拡張
- XML形式(.vtu/.vtp)対応
- バイナリモード対応
- 複数スカラー／ベクトルデータセット出力の強化 