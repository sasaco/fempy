# PQ-06 VTK出力の実装・検証報告

実施日: 2026-09-09  
基準コミット: `6085418c53f03f200c2c0a34609973ae021e3653`  
状態: 完了（変更は未コミット）

## 結論

Legacy ASCII VTKのセル型、節点順、元ID、点／セルデータの対応を修正し、独立readerで読み戻した。
`bar`／`nonlinear_bar`、3／4節点`shell`、一次・二次`tetra`／`wedge`／`hexa`を扱う。
未対応型や要素型と節点数の不一致は、セル型0や空セルとして成功させず`ValueError`にする。

全2,003件のpytest、Wiki全18実行例、`meshio`による7件の読戻し、公式VTK 9.7による
15節点wedgeの読戻しが成功した。廃止済みの仮想梁シェル断面力は復活させていない。

## REDで固定した問題

変更前に`tests/io/test_vtk.py`を追加し、6件すべての失敗を確認した。

- `shell`、`nonlinear_bar`、`tetra2`、`wedge2`、`hexa2`がセル型0になり、readerが拒否した。
- `CELL_DATA`を結果辞書のキーだけで書くため、2セルに1結果を渡すと配列長1の不正ファイルになった。
- 梁端力・ソリッド積分点配列を`float()`へ変換して例外になった。
- 未対応型と不正節点数を型0として保存し、成功扱いにした。
- 元の節点ID・要素IDを保存しないため、非連続IDの対応を読戻し後に追跡できなかった。

## 実装した公開契約

### トポロジ

| FEMPython要素 | VTKセル | 番号 |
|---|---|---:|
| `bar`, `nonlinear_bar` | line | 3 |
| 3節点`shell` | triangle | 5 |
| 4節点`shell` | quad | 9 |
| `tetra`, `hexa`, `wedge` | tetra, hexahedron, wedge | 10, 12, 13 |
| `tetra2`, `hexa2`, `wedge2` | quadratic tetra/hexahedron/wedge | 24, 25, 26 |

節点と要素をID順に書き、`node_id`と`element_id`もデータ配列へ保存する。
二次要素はFEMPythonの中間節点を落とさず出力する。VTK 9.7.0の
`vtkWedge.GetParametricCoords()`と`vtkQuadraticWedge.GetParametricCoords()`を実行し、
現行FEMPythonの局所節点順と一致することを確認した。

### 結果量

- `displacement`: 全体座標の節点並進変位。
- `reaction_force`: 全体座標の節点反力。反力未定義節点は`NaN`。
- `section_force_i_*`, `section_force_j_*`: 梁両端の力・モーメント6成分。
- `shell_stress_surface_1/2`, `shell_strain_surface_1/2`: シェル両面の全体座標テンソル。
- `shell_membrane_*`, `shell_moment_*`, `shell_shear_*`: シェル局所座標の断面合力。
- `solid_stress_gauss_point_mean`, `solid_strain_gauss_point_mean`: ソリッド積分点の成分別単純平均。

全データ配列を出力セルID順へ整列する。量が定義されないセルは0でなく`NaN`とする。
ソリッドのGauss点単純平均は、体積重み付き平均、セル中心値、節点外挿値ではない。

## 独立検証

開発依存に`meshio 5.3.5`を追加し、製品の実行依存は増やしていない。`meshio`はVTK type 26を
`wedge15`へ復号する一方、同版の次元表に`wedge15`が欠けるため、テスト側で次元メタデータだけを
補った。セル型・接続・データの解析処理には手を加えていない。また同版はVTK 9.7より前の
線形wedge順序へ正規化するため、その既知の置換をテスト比較時だけ元へ戻した。

```powershell
uv run --locked --extra dev pytest tests/io/test_vtk.py -q
```

結果: `7 passed in 0.22s`。

検証内容:

- 10種類の混在セル、非連続節点／要素ID、挿入順と異なるID順。
- 二次tetra／wedge／hexaの全中間節点。
- 変位、反力、梁端力、シェル両面テンソルと断面合力、ソリッド応力テンソル。
- 一部セルだけにある量が0でなく`NaN`となること。
- 未対応型、5節点shell、9節点tetra2の明示失敗。
- 公開`FemModel.run()`による4節点shellの実解析結果。

公式VTK 9.7.0も一時環境だけで実行し、出力した15節点wedgeを
`vtkUnstructuredGridReader`で読んだ。点15、セル1、セル型26、接続0〜14、
`node_id`、`element_id=307`が一致した。VTKは製品・開発依存へ追加していない。

参照:

- [VTK Legacy file format](https://docs.vtk.org/en/v9.7.0/vtk_file_formats/vtk_legacy_file_format.html)
- [VTK 9.7 wedge ordering修正](https://docs.vtk.org/en/v9.7.0/release_details/9.7/fix-wedge-point-ordering.html)

## 回帰検証

```powershell
uv run --locked --extra dev python -m py_compile src/fem/vtk_writer.py src/fem/file_io.py
uv run --locked --extra dev pytest tests/io tests/postprocess tests/integration/test_model_contracts.py -q
uv run --locked --extra dev python -m tools.validation.check_wiki
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq06-current.xml
```

- py_compile: 終了コード0。
- I/O・後処理・モデル契約: `142 passed in 6.89s`。
- Wiki: 13ページ、Python 19ブロック、JSON 17ブロック、ローカルリンク、実行例18件が成功。
- 全件: `2003 passed in 1083.53s`、終了コード0。
- JUnit: tests 2,003、failures 0、errors 0、skipped 0、time 1,083.514秒。

## 残る適用範囲

- Legacy ASCII VTKだけを対象とし、XML VTK、バイナリ、時系列・モードアニメーションは未対応。
- 荷重・支持条件のglyph、シェル辺合力、ソリッド積分点ごとの値は直接保存しない。
- `pyramid`、旧stubの`hexa20`など解析前検証で未対応の要素はVTKでも拒否する。
- `NaN`を表示・除外する方法は可視化ソフト側の機能に依存する。
- VTK 9.7でwedge規約が修正されたため、古い規約を固定したreaderは三角形面の向きを
  異なる順へ正規化する場合がある。本実装は現行VTK 9.7のパラメトリック節点順を正本とする。
