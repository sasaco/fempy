# PQ-12 性能基準・計測報告

実施日: 2026-09-10  
対象: `main` / `1057788` に本項目の未コミット変更を重ねた作業ツリー

## 結論

梁・シェル・ソリッドの小／中／大モデルと、5／20／50 stepの材料非線形履歴を、
同じ入力を再生成できる専用ツールで測定した。通常梁とは別に高精度梁経路を1ケース測り、
組立、境界処理、求解、後処理、モデル／結果JSON、VTK、Python peak、process RSSを保存した。

通常pytestには時間閾値を置かず、モデル・JSON・回帰判定の契約だけを検査する。同じ環境fingerprintの
専用計測だけが保存済み閾値と比較でき、環境またはworkloadが異なる場合は性能合否を出さず失敗する。
今回は基準確立が目的であり、製品ソルバーの最適化やキャッシュ追加は行っていない。

生の5反復値、位相別時間、環境、閾値は
[PQ-12 baseline JSON](product-quality-pq12-baseline.json)に保存した。

## 測定環境と条件

- Windows 11 `10.0.26200`、AMD64、論理CPU 20
- Intel64 Family 6 Model 186 Stepping 2
- CPython 3.13.11、NumPy 2.4.1、SciPy 1.17.0
- `OMP_NUM_THREADS`、`OPENBLAS_NUM_THREADS`、`MKL_NUM_THREADS`、
  `NUMEXPR_NUM_THREADS`をすべて1へ固定
- `time.perf_counter()`、5反復の中央値、事前warmup 1回
- Python allocation peakは`tracemalloc`、process RSSは5 ms間隔でsample
- 計測器によるoverheadを含む。同じツール・環境での回帰判定用であり、対話利用の絶対待ち時間を
  そのまま予測する値ではない

## 基準値

時間は5反復の中央値、メモリは5反復の最大増分である。`case`は同一モデルで順次解く荷重case数、
`step`は材料非線形の履歴step数、`HP`は高精度経路へ入った解析回数を示す。

| workload | 節点 | 要素 | DOF | nnz | case | step | HP | 中央値 s | Python peak MiB | RSS増分 MiB | 最大位相 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| beam-small | 17 | 16 | 102 | 430 | 1 | 0 | 0 | 0.075 | 0.2 | 0.1 | 組立 0.026 s |
| beam-medium | 65 | 64 | 390 | 1,678 | 3 | 0 | 0 | 0.756 | 0.6 | 0.6 | 組立 0.324 s |
| beam-large | 257 | 256 | 1,542 | 6,670 | 5 | 0 | 0 | 5.760 | 2.6 | 5.5 | 組立 2.469 s |
| precision-small | 17 | 16 | 102 | 430 | 1 | 0 | 1 | 0.368 | 1.0 | 0.4 | 求解 0.272 s |
| shell-small | 6 | 2 | 36 | 504 | 1 | 0 | 0 | 0.250 | 0.4 | 0.1 | 後処理 0.192 s |
| shell-medium | 28 | 18 | 168 | 3,420 | 2 | 0 | 0 | 4.062 | 2.5 | 3.8 | 後処理 3.529 s |
| shell-large | 91 | 72 | 546 | 12,654 | 3 | 0 | 0 | 23.809 | 7.1 | 13.1 | 後処理 20.927 s |
| solid-small | 8 | 1 | 24 | 558 | 1 | 0 | 0 | 0.034 | 0.2 | 0.0 | 求解 0.009 s |
| solid-medium | 27 | 8 | 81 | 3,009 | 2 | 0 | 0 | 0.330 | 0.5 | 0.2 | 組立 0.132 s |
| solid-large | 64 | 27 | 192 | 8,738 | 3 | 0 | 0 | 1.492 | 1.3 | 2.2 | 組立 0.633 s |
| history-small | 3 | 2 | 18 | 66 | 1 | 5 | 0 | 0.116 | 0.3 | 0.0 | 組立 0.059 s |
| history-medium | 5 | 4 | 30 | 118 | 1 | 20 | 0 | 0.693 | 1.0 | 0.5 | 組立 0.451 s |
| history-large | 9 | 8 | 54 | 222 | 1 | 50 | 0 | 3.014 | 3.2 | 2.7 | 組立 2.222 s |

## 回帰閾値

各workloadは測定時に次をJSONへ固定する。

- wall: `max(最大値×1.10, 中央値×1.25, 中央値+6×MAD)`
- Python peak: `最大値 + max(1 MiB, 最大値×0.10)`
- RSS増分: `最大値 + max(4 MiB, 最大値×0.20)`

閾値はこの環境の実測ばらつきに対する回帰検出用であり、異なるCPU、OS build、Python、
NumPy、SciPy、thread設定には転用しない。環境fingerprint、case ID、節点・要素・DOF・nnz、
荷重case、履歴step、高精度経路回数のいずれかが違う場合も比較を拒否する。

## 再現と比較

基準の再生成:

```powershell
$env:OMP_NUM_THREADS='1'
$env:OPENBLAS_NUM_THREADS='1'
$env:MKL_NUM_THREADS='1'
$env:NUMEXPR_NUM_THREADS='1'
uv run --locked --extra dev python -m tools.validation.performance `
  --profile baseline --repeats 5 --warmups 1 `
  --output tmp/product-quality-pq12-current.json
```

保存済み基準との比較:

```powershell
uv run --locked --extra dev python -m tools.validation.performance `
  --profile baseline --repeats 5 --warmups 1 `
  --output tmp/product-quality-pq12-current.json `
  --compare docs/report/product-quality-pq12-baseline.json
```

異なる環境では新しい基準を別artifactとして保存し、既存JSONを上書きして差を隠さない。
通常CIは次の高速な契約試験を実行する。

```powershell
uv run --locked --extra dev pytest tests/validation/test_performance.py -q
```

## 検証結果

- 性能契約: 3成功、失敗0、skip 0
- PQ-09／PQ-10を含む集中試験: 12成功、失敗0、skip 0
- Wiki: 13ページ、Python 21 block、JSON 17 block、実行例18件成功
- pytest収集: 66 test files、386関数、2,026展開caseで台帳と一致
- 全件: 2,026成功、failure 0、error 0、skip 0、終了コード0
- JUnit実測: 1,265.754秒（21分06秒）、Python 3.13.11、pytest 9.0.2
- baseline: 13 workload、tool SHA-256と保存provenanceの一致を確認
- `py_compile`、JSON parse、Markdown表13列、`git diff --check`成功

全件JUnitは`tmp/product-quality-pq12-current.xml`、基準実測は
`docs/report/product-quality-pq12-baseline.json`にある。`tmp`は配布成果物ではない。

## 観察したボトルネックと次の候補

- 通常梁はこの範囲で剛性組立が最大位相だった。5荷重caseのlargeでも高精度移行は0回である。
- 高精度梁は同一102 DOFの通常梁より約4.9倍の総時間で、求解・釣合い回復が最大位相だった。
  高精度を通常経路へ誤って広げる変更は性能回帰として独立検出できる。
- シェルは後処理が支配的で、largeでは中央値の約88%を占めた。表面量、局所断面合力、
  legacy viewを同時に構築する経路を、正しさと公開schemaを保ったままprofileする価値がある。
- ソリッドはlargeで剛性組立と求解がほぼ同程度だった。
- 材料非線形履歴はstepごとの接線剛性・内力組立が支配的だった。履歴を無視するcacheは使えない。

最適化へ進む場合は、まずシェル後処理と非線形の反復組立を関数単位でprofileする。
線形複数荷重の分解再利用は、剛性・支持・材料・要素状態が不変であることを識別できる設計と、
各caseを独立に解いた結果との一致を用意してから実装する。本項目では変更検出なしのcacheを導入しない。

## 保証範囲

この基準は上表の構造・規模・単一process・単一thread設定に限る。最大節点数や最大DOFを
製品上限とは定義しない。並列実行、長時間のmemory leak、HTTP同時実行、他の要素定式化、
異なる疎行列backend、GPUは未測定である。モデル規模の可否は節点数だけでなく、DOF、nnz、
履歴step、結果量、実行環境の制約で判断する。
