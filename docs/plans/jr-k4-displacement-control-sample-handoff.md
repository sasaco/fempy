# JR K4 変位制御サンプル修正 handoff

更新日: 2026-09-09

状態: **完了。moment_z／端モーメント rz／制御 DOF rz の曲げモデルへ再設計済み。**

## 2026-09-09 完了結果

- `tests/data/snap/jr_k4_displacement_control.json` を純曲げ片持ち梁へ変更し、原点を含む 10 step の `curvature.7.z = [0, .0005, .001, .002, .004, .007, .010, .014, .018, .020]` を保存した。
- 製品出力のコピーではなく、`tests/support/oracles/jr_k4_bending.py` の Decimal 区分線形 JR 骨格曲線と片持ち梁静力学から、lambda、横変位、回転、反力、端モーメント、曲率の全保存値を導出する。
- step 8 は K4 内、step 9 は P4、step 10 は δ4 超過後の同一 K4 直線を検証する。旧・全 step 曲率 0 の履歴と step 8～10 の主要値改変は regression comparator が拒否する。
- Python 直接構築、JSON 読込、HTTP の三経路で同じ 10 step の物理履歴を確認した。
- canonical LF SHA-256 は `67749fec183ed8ae1d4a5058f5b3e572e40cad59252a0cc4b230d0f0ee1b1ee1`。
- 検証: 履歴回帰・入力三経路・suite contract の対象 26 件成功、材料非線形マーカー 901 件成功（1077 deselected）、`git diff --check` 成功。

以下は修正前の問題と設計判断を残した履歴である。

## 2026-09-09 追加レビュー（最優先）

ユーザーから、`result["1"]`～`result["10"]` のすべてが

```json
"curvature": {"7": {"y": 0.0, "z": 0.0}}
```

になっているのは誤りだと指摘された。直前の修正では、欠落していた `curvature` を全 step に追加しただけで、すべて 0 を保存して回帰比較するテストまで追加してしまった。この状態を完成扱いしてはいけない。

現在の入力は次の純軸モデルなので、製品の `curvature` 定義上、曲率が 0 になること自体は実装どおりである。

- `hysteresis_dofs: ["axial"]`
- 荷重は節点30の `tx`
- 変位制御は節点30の `dx`
- `curvature` は軸ひずみやねじれ角ではなく、部材中央の全曲げ曲率 `y/z` だけを表す

したがって、JSON の期待値だけを非ゼロへ書き換えてはいけない。また、製品の `curvature` に軸ひずみを混ぜる変更もしてはいけない。サンプル自体を曲げの JR K4 変位制御モデルへ作り直す必要がある。

推奨する修正は、部材7の `hysteresis_dofs` を `moment_z`、基準荷重を節点30の `rz: 1`、変位制御 DOF を節点30の `rz` にすることである。長さ `L=2` の片持ち梁に端モーメントを作用させるため、制御回転を `theta_z` とすると曲率は `kappa_z=theta_z/L` になる。現在の targets をそのまま回転として用いる場合、保存すべき曲率候補は次のとおり。

```text
target rz:   [0, .001, .002, .004, .008, .014, .020, .028, .036, .040]
curvature z: [0, .0005, .001, .002, .004, .007, .010, .014, .018, .020]
lambda=M:    [0, 5, 10, 12, 16, 19, 22, 18, 14, 12]
```

ただし、符号と節点横変位・反力・section-cut moment は製品出力からコピーせず、片持ち梁の純曲げ式と既存の section-cut 符号規約から独立に導出すること。`tests/integration/test_curvature_output.py` と `tests/support/builders/nonlinear_reference.py` の `moment_z` 例を参照できる。

### 追加レビュー後の現在地

- `displacement_control_history` 契約、10 targets、全 step の保存・1回解析による比較は実装済み。
- `tests/support/sample_runner.py` は `curvature` も再帰比較する状態。
- `tests/regression/test_displacement_control_history.py` は現在、全 step の曲率が 0 であることを要求している。この誤った assertion を最初の RED 対象として置き換える。
- 現在の fixture SHA は `4a039e9dfb41c34bc4e0568ae7bd36a69a4ff8db296c9a5c9cf27aa268a52647`。これは全曲率0の途中状態の hash であり、修正後に manifest を更新する。
- 直前の途中状態では対象24件、材料非線形900件、一般保存323ケース＋新履歴5件の合計328件が成功した。ただし全曲率0を正しいとするテストを含むため、受入完了の証拠にはしない。

## 目的

`tests/data/snap/jr_k4_displacement_control.json` を、曲げ曲率によって原点から JR 総研剛性低減 RC 型の K1、K2、K3、K4 各領域を単調載荷で通過し、`displacement_control.targets` の全点について非ゼロの曲率履歴を含む結果を保存・回帰比較するサンプルへ修正する。

## 現在の問題

- 履歴専用契約と10 targetsへの変更は完了したが、入力が純軸のままなので `result["1"]`～`result["10"]` の曲率がすべて 0 である。
- 全曲率0を fixture とテストが正解として固定しており、K1～K4を曲率履歴として確認できない。
- 曲げモデルへ変更すると `disg`、`reac`、`fsec` の対象成分と期待値も変わるため、曲率だけを局所的に書き換えてはいけない。
- `lambda`、`control_displacement`、step ID、1回解析の履歴契約は維持する。

## 理論値と推奨 targets

モデルは長さ `L=2`、`Iz=1`、基準端モーメント `rz=1`、JR履歴対象 `moment_z` とする。材料点は次のとおり。

- `(delta_1, P_1) = (0.001, 10)`
- `(delta_2, P_2) = (0.004, 16)`
- `(delta_3, P_3) = (0.010, 22)`
- `(delta_4, P_4) = (0.018, 14)`
- `K1=10000`, `K2=2000`, `K3=1000`, `K4=(14-22)/(0.018-0.010)=-1000`

曲率は `delta=kappa_z=theta_z/L` なので、次の単調な制御回転 targets を使う。

| step | target `theta_z` | `delta=kappa_z=theta_z/L` | 位置 | 期待 `lambda=M` |
|---:|---:|---:|---|---:|
| 1 | 0.000 | 0.0000 | 原点 | 0 |
| 2 | 0.001 | 0.0005 | K1 内 | 5 |
| 3 | 0.002 | 0.0010 | P1 | 10 |
| 4 | 0.004 | 0.0020 | K2 内 | 12 |
| 5 | 0.008 | 0.0040 | P2 | 16 |
| 6 | 0.014 | 0.0070 | K3 内 | 19 |
| 7 | 0.020 | 0.0100 | P3 | 22 |
| 8 | 0.028 | 0.0140 | K4 内 | 18 |
| 9 | 0.036 | 0.0180 | P4（K4 勾配の参照点） | 14 |
| 10 | 0.040 | 0.0200 | delta4 超過後も同じ K4 直線 | 12 |

`delta_4` は K4 枝の終端ではなく勾配を定義する参照点である。step 8 で K4 領域内、step 9 で P4、step 10 で delta4 超過後の K4 継続を確認する。

各 step では少なくとも以下を独立に比較する。

- `control_displacement == target`
- `lambda == P`
- 節点30の `rz == target`
- `curvature.7.z == target/L`、`curvature.7.y == 0`
- 節点10の `mz` が端モーメントと釣り合う
- 部材7の `mzi` と `mzj` が section-cut 規約に従って端モーメントと釣り合う
- 軸力、せん断力、ねじり、面外曲げ成分は 0
- 節点30の横変位は純曲げの独立式から求めて全 step で比較する

## 推奨する保存形式と回帰契約

既存の `section_cut_cases` の `result` キーは荷重ケース ID であり、target の step ID と兼用してはいけない。新しい `displacement_control_history` 契約を設ける。

- manifest の `cases` は空配列にする。
- manifest の `steps` は `"1"` から `"10"` とする。
- JSON の `result` は同じ step ID で 10 個保存する。
- 各 step の保存値に `lambda` と `control_displacement` を含める。
- `disg`、`reac`、`fsec` は曲げモデルの全対象成分を保存する。
- `curvature` は必須比較対象とし、各 step の `7.z` が上表の `delta` と一致することを確認する。全step 0を許可しない。
- `shell_results`、`size` はこの履歴契約の比較対象へ含めない。
- 解析は 1 回だけ実行し、返された `step_results` と保存済み step を順番・件数込みで再帰比較する。step ごとに解析をやり直して材料履歴を失わないこと。

期待値は solver の現在出力からコピー・再生成せず、上表の区分線形式と静力学的釣合いから作る。必要なら `tests/support/oracles/` に製品コードを import しない小さな純粋関数を置く。

## 変更対象

最低限、次を変更する。

1. `tests/data/snap/jr_k4_displacement_control.json`
   - `axial`/`tx`/`dx` を `moment_z`/`rz`/`rz` へ変更する。
   - 10 step の `result` を曲げ変位・反力・断面力・非ゼロ曲率履歴へ置換する。
2. `tests/data/manifest.json`
   - contract を `displacement_control_history` に変更する。
   - `cases: []`、`steps: ["1", ..., "10"]` とする。
   - JSON 修正後の canonical LF SHA-256 へ更新する。
3. `tests/support/samples.py`
   - 新契約を履歴回帰として収集する。
4. `tests/support/sample_runner.py` または専用 runner
   - 1 回の解析から全 step を比較する。
5. `tests/regression/`
   - 新しい保存履歴契約を pytest の個別テストとして公開する。
6. `tests/harness/test_suite_contracts.py`
   - `steps` と JSON の保存 step が一致することを検査する。
   - 一般ケース数は新サンプルを除外するため `323` に戻る想定。
7. `tests/integration/test_input_routes.py`
   - 動的 builder だけに閉じず、保存 JSON の入力と期待履歴も使用する。ただし Python/JSON/HTTP の三経路同値性は維持する。
8. `tests/README.md`
   - 46 モデルを「一般 FEM 44 モデル・323 ケース」「変位制御 1 モデル・10 targets」「片持ち梁 1 モデル・101 保存段階」と区別して記載する。

## TDD と受入条件

先に次の RED を追加してから fixture/runner を直す。

- `len(result) == len(displacement_control.targets) == 10`
- result の step ID が `1..10` で欠落・重複しない。
- 全 step の `control_displacement` と target が一致する。
- 全 step の lambda が `[0, 5, 10, 12, 16, 19, 22, 18, 14, 12]` と一致する。
- 全 step の `curvature.7.z` が `[0, .0005, .001, .002, .004, .007, .010, .014, .018, .020]` と一致し、`curvature.7.y == 0` である。
- step 8、9、10 のいずれかの lambda/反力/曲げモーメント/曲率を改変すると回帰テストが失敗する。
- 全stepの曲率が0である旧fixtureを明示的に失敗させる。
- 元の Python/JSON/HTTP 変位制御テストも通る。
- manifest の未登録 JSON、SHA、件数契約がすべて通る。

実行候補:

```powershell
uv run --project . --extra dev pytest -q tests/integration/test_input_routes.py
uv run --project . --extra dev pytest -q tests/regression -k "jr_k4 or displacement_control"
uv run --project . --extra dev pytest -q tests/harness/test_suite_contracts.py
git diff --check
```

## 作業ツリー上の注意

- この handoff 作成時の HEAD は `aedbd26`。
- 現在の `jr_k4_displacement_control.json`、manifest、件数契約、README の追加・変更は未コミットであり、本 handoff の要件を満たしていない途中状態である。
- 作業ツリーには本件以外の変更が多数ある。特に `docs/plans/product-quality-handoff.md` など既存の未追跡・変更済みファイルを上書きしたり、本件コミットへ混ぜたりしないこと。
- 前の K4/変位制御本体実装は commit `544c5c8`。K4 は delta4 超過後も同じ直線を延長する仕様を維持する。
