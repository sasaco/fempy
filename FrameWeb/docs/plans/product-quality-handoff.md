# プロダクト品質ロードマップ引継ぎ

更新日: 2026-09-10

対象計画: [プロダクト品質の修正ロードマップ](product-quality-roadmap.md)

## 結論

PQ-00〜03・PQ-05〜10・PQ-12は完了した。PQ-04はユーザー判断で保留し、段階2は未完了のまま維持する。
段階3「利用時の信頼性」では、機械可読な機能対応表、未対応組合せの解析前拒否、
検証済みLegacy VTK出力、安定した解析診断と結果メタデータを実装した。段階4では
PQ-09のメッシュ誤差と適用範囲、PQ-10の単位不変な非線形収束判定を完了した。
PQ-11は実験資料待ちである。これで外部接続または資料を必要としないロードマップ項目は完了した。

## 今回完了した内容

### PQ-01: 固有値解析

- 三角形シェル質量を二次被積分関数に適合する3点積分へ修正した。
- 実三角形シェル、一次・二次四面体を閉形式の固有値で検証した。
- Wikiの両端ばね支持梁をテストへ固定した。
- 81節点・128三角形・432自由DOFの疎行列モデルで、通常経路とARPACK失敗後の
  シフト反転再試行が同じ低次4モードを返すことを確認した。
- 固有対残差、質量直交性、拘束DOF復元、CP932標準出力を回帰試験へ含めた。

詳細: [PQ-01実装・検証報告](../report/product-quality-pq01.md)

### PQ-03: パッケージとバージョン

- `src/fem/_version.py`の1.0.2を単一のバージョン源にした。
- `pyproject.toml`を動的versionへ変更し、競合していた`setup.py`を廃止した。
- `requirements.txt`は`-e .[dev]`で正本へ委譲する。
- Python 3.13.11の隔離venvへwheelを入れ、`fem`・`app` import、静解析、モデル・結果保存を確認した。
- HTTPはリポジトリ／コンテナ入口とし、`main.FrameWeb3`を`main.FEMPython`の互換aliasとして残した。

詳細: [PQ-03実装・検証報告](../report/product-quality-pq03.md)

### PQ-05: 導入文書

- READMEを現行公開import、単位付き独立解、対応範囲、実在するコマンドへ更新した。
- 公開Wiki URL `https://sasaco.github.io/fempy/` のHTTP 200を確認した。
- 文字化けしていた`test_plan.md`を現行のテスト台帳とロードマップに沿って再作成した。
- wheelのMETADATAと隔離環境でREADME相当の解析・保存を検証した。

詳細: [PQ-05導入文書・品質説明の検証報告](../report/product-quality-pq05.md)

### PQ-07: 機能対応表と解析前検証

- `src/fem/capabilities.json`を配布wheelにも含む唯一の正本とした。
- 11要素型、3解析種別、質量行列、8荷重種別、6結果種別を
  `verified`／`implemented`／`unsupported`で登録した。
- 公開要素名と旧V0名を正本で解決し、実際に生成した完全修飾クラス名も照合する。
- 一次wedgeの固有値解析、pyramid／hexa20の全解析、ソリッド面圧を、
  ソルバー組立前に要素ID・型・理由付きで拒否する。
- READMEとWikiの表を同じJSONから生成し、`check_wiki`で同期漏れを失敗にする。
- `get_capability_registry()`等を公開し、利用者も機械可読な定義を取得できる。

全1,996件は失敗・skipなしで成功した。詳細: [PQ-07実装報告](../report/product-quality-pq07.md)

### PQ-06: VTK出力

- `shell`、`nonlinear_bar`、`tetra2`、`wedge2`、`hexa2`を含む対応要素を正しいセル型にした。
- 3／4節点shellを型分けし、二次要素の中間節点順をVTK 9.7のパラメトリック座標と照合した。
- `node_id`と`element_id`を保存し、非連続ID・混在セル・結果辞書順によらずデータを整列する。
- 変位、反力、梁端力、シェル両面テンソル／局所断面合力、ソリッドGauss点単純平均を分離した。
- 欠損値は`NaN`とし、未対応型・不正節点数をセル型0で成功させず明示的に失敗する。
- `meshio`による7回帰、公式VTK 9.7の二次wedge読戻し、実シェル解析の結果読戻しを確認した。

詳細: [PQ-06実装・検証報告](../report/product-quality-pq06.md)

### PQ-08: 診断と結果メタデータ

- `invalid_input`、`unsupported_analysis`、`structural_mechanism`、
  `numerical_ill_conditioning`、`nonlinear_nonconvergence`、`modal_nonconvergence`を
  `ValueError`／`RuntimeError`互換の診断例外として追加した。
- HTTPエラーを`error`、`error_code`、`error_category`、`converged`、任意の`details`へ統一した。
- 未対応機能の要素ID、直接確認できた行列自由度、非線形の失敗step／load factorを返す。
  一般的な特異行列から原因節点を推測しない。
- 3解析の成功結果にschema／製品version、入力SHA-256、解析条件、座標・一貫単位系、
  残差、反復数、警告、高精度経路を持つ`metadata`を追加した。
- `model_metadata`と結果`metadata`のJSON保存往復、入力変更時のhash変更を固定した。
- `src/fem`内の無条件`print`を標準loggingへ移し、ログレベルで制御できるようにした。

詳細: [PQ-08実装・検証報告](../report/product-quality-pq08.md)

### PQ-09: メッシュ誤差と適用範囲

- 製品値から期待値を生成せず、梁・Kirchhoff／Mindlin板の閉形式解とHexa8製造解を実装した。
- 2・4・8分割の変位L2、応力／曲げモーメントL2、ひずみエネルギー誤差と収束率を測った。
- 梁は4/2/4次、DKTは約2/1/2次、MITC4は約2/1/4次、Hexa8は2/1/2次で収束した。
- 歪み0.4要素幅、板アスペクト比4、板厚100倍、Hexa8奥行比100倍の感度を測った。
- MITC4のストリップ問題では薄板化による誤差増大を確認しなかったが、任意形状へ一般化しない。
- Hexa8は`nu=0.49`以上で応力・エネルギー誤差が急増し、非圧縮極限を適用制限とした。
- 点荷重・角部などの応力特異性、未測定の要素・定式化を保証範囲から明示的に除いた。

詳細: [PQ-09メッシュ誤差・適用範囲の検証報告](../report/product-quality-pq09.md)

### PQ-10: 単位換算と非線形収束判定

- 修正前に、同じ曲げ問題がN–mで4反復、N–mmで5反復、kN–mで未更新のまま1反復成功する
  単位依存を再現した。
- 座標spanの代表長さ`L`で、一般化力を`[F,M/L]`、一般化変位を`[u/L,theta]`へそろえた。
- 相対残差の尺度は現在外力、復元力、未倍率の基準荷重パターンから作り、入力単位の固定値を
  分母に使わない。荷重係数0の除荷でも尺度を失わない。
- 履歴を持たない直接静解析のメタデータ残差も外力－内力－ばね力へ統一し、専用回帰を追加した。
- N–m、N–mm、kN–mで材料骨格、断面、密度、荷重、ばね、強制変位を一貫換算した。
- 荷重制御、循環載荷、変位制御、強制変位、固有値で物理応答と成功判定を比較した。
- 3単位系で曲げは4反復、循環7段階は`[3,4,4,3,4,4,3]`、変位制御は4反復で一致した。

詳細: [PQ-10単位換算・収束判定報告](../report/product-quality-pq10.md)

### PQ-12: 性能基準と計測

- 通常梁、シェル、ソリッドの小／中／大、材料非線形5／20／50 stepを測定した。
- 通常梁と分けて高精度梁を測り、高精度経路へ入った解析回数も保存した。
- 13 workloadについてDOF、nnz、荷重case、履歴step、位相別時間、Python peak、RSSを記録した。
- 1 thread、warmup 1回、5反復中央値の環境付きbaseline JSONを保存した。
- 同じ環境fingerprint・workloadだけを実測ばらつき由来の閾値へ比較するCLIを追加した。
- 通常pytestは時間閾値を使わず、smoke model、schema、保存baseline、比較器の契約を検査する。
- 最大位相は、通常梁が組立、高精度梁が求解、シェルが後処理、材料非線形が反復組立だった。
- 製品コードの最適化・cacheは行わず、性能基準を先に固定した。

詳細: [PQ-12性能基準・計測報告](../report/product-quality-pq12.md)

### PQ-00: clean checkoutの追補

`ebd64ea`単体の全件実行は`1924 passed, 2 failed`だった。2失敗は
`sampleBendTetra1.fem`の固定原本SHA-256がcheckoutのCRLF/LF差に依存していたためである。
また、旧`docs/v0/** text eol=lf`はPDF・PNG・XLSXまで変換対象にしていた。

- 固定原本SHA-256は改行をLFへ正規化して比較する。
- PDF・PNG・XLSXは`.gitattributes`で`-text`とする。
- 修正後の原本監査46件はすべて成功した。

詳細: [PQ-00〜02実装・検証記録](../report/product-quality-pq00-pq02.md)

## 最終検証

基準HEAD `aedbd26b62607e11b2e80744e449518da567db0e`へ、上記プロダクト品質変更だけを
重ねたclean Git worktreeを作成して実行した。

```powershell
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-current-clean.xml
```

- Python 3.13.11 / pytest 9.0.2
- 1,972成功、失敗0、skip 0
- 終了コード0
- 1,134.23秒（18分54秒）

集中試験はモーダル・シェル97件、パッケージ／HTTP 23件、原本監査46件が成功した。
Wikiチェッカーは13ページ、Python 18ブロック、JSON 17ブロック、実行例18件が成功した。
最終wheelは`fempython-1.0.2-py3-none-any.whl`、SHA-256は
`8dd12585bcadb3ff905bf6bd96954ece1483163b9f0bd07c012d2779845682a9`である。

## 共有作業ツリーの注意

PQ-12開始時は`main`の`1057788`で作業ツリーはclean、`origin/main`より2コミット先行していた。
今回の変更は性能計測器、その契約試験、保存baseline、README、テスト保証範囲、
ロードマップ／引継ぎ／報告に限定した。製品ソルバーは変更していない。commit、pushは行っていない。
次回開始時も`git status --short`と`git diff`を取り直し、他セッションの変更をstash、reset、整形、
commitしない。

## PQ-04実装内容と残る外部接続

実装:

- `.github/workflows/tests.yml`
- `.github/workflows/publish-pypi.yml`
- `tools/validation/check_distribution.py`
- `tools/validation/check_wheel_install.py`
- `tests/harness/test_release_workflows.py`

成立した契約:

1. 全件pytest、Wiki例、wheel隔離導入、メタデータ試験のいずれかが失敗したら公開しない。
2. 検証jobが作ったwheelをartifactとして公開jobへ渡し、公開直前に再ビルドしない。
3. 検証対象コミット、wheelファイル名、SHA-256をCI成果物へ残す。
4. `v<version>`タグだけを発火条件とし、同一versionが存在すればskipせず失敗する。
5. PyPI token secretを使わず、`pypi` environmentとjob限定OIDCで公開する。

ローカルではactionlint 1.7.12、workflow契約／改変検出5件、manifest生成・再照合、
Python 3.13.11の隔離wheel解析が成功した。再ビルドしたwheel SHA-256は前回と同じ
`8dd12585bcadb3ff905bf6bd96954ece1483163b9f0bd07c012d2779845682a9`だった。
PQ-04全件実行時の共有作業ツリーでは1,982成功・失敗0・skip 0・終了コード0、17分58秒、
材料非線形は902成功・終了コード0、44.39秒だった。JUnitは
`tmp/product-quality-pq04-current.xml`と`tmp/product-quality-pq04-material.xml`にある。

PyPIには2026-01-30公開の`FEMPython 1.0.2`が既にあり、そのwheel SHA-256は
`91df6e78f893ca0dce39da292a23e677e7154df432a7f6007dc9189a8bd0d362`で、現行wheelと異なる。
したがって現行versionのタグを作らない。実リリース時は`src/fem/_version.py`を未使用versionへ
更新し、commit、push、`pypi` environmentのTrusted Publisher設定後にタグを作成する。
GitHub APIで確認できたenvironmentは`github-pages`だけであり、`pypi`は未作成だった。
今回はGitHub上のworkflow実行、外部設定、commit、push、タグ作成、PyPI公開を行っていない。

詳細: [PQ-04 CI・PyPI公開経路報告](../report/product-quality-pq04.md)

## PQ-06最終検証

```powershell
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq06-current.xml
```

- Python 3.13.11 / pytest 9.0.2 / NumPy 2.4.1 / SciPy 1.17.0 / meshio 5.3.5
- 2,003成功、失敗0、error 0、skip 0、終了コード0
- JUnit実測1,083.514秒、pytest表示1,083.53秒（18分03秒）
- Wiki 13ページ、Python 19ブロック、JSON 17ブロック、実行例18件が成功
- 公式`vtk==9.7.0`による15節点wedgeの型26・接続・ID読戻しが成功

## PQ-08集中検証

```powershell
uv run --locked --extra dev pytest tests/io tests/solvers tests/integration/test_model_contracts.py tests/integration/test_beam_precision.py -q
```

- 関連205成功、失敗0、skip 0、終了コード0
- 診断・メタデータ固有試験は9成功
- `rg -n "print\\(" src/fem --glob "*.py"`は該当0件

全件は次で実行した。

```powershell
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq08-current.xml
```

- Python 3.13.11 / pytest 9.0.2
- 2,012成功、failure 0、error 0、skip 0、終了コード0
- JUnit実測1,090.000秒、pytest表示1,090.01秒（18分10秒）
- Wiki 13ページ、Python 21ブロック、JSON 17ブロック、実行例18件が成功

## PQ-09集中検証

```powershell
uv run --locked --extra dev pytest tests/validation/test_mesh_convergence.py -q
```

- 4成功、失敗0、skip 0、終了コード0
- 補正後の最終確認は15.77秒
- 同じ測定値は`python -m tools.validation.mesh_convergence`でJSONへ再生成できる

全件試験はPQ-09のアスペクト比表記補正前に2,017成功・失敗0・skip 0、終了コード0、
1,159.048秒（19分19秒）だった。補正はPQ-09新規メッシュのy方向分割数だけで、製品コードと
既存試験を変更していない。補正後のPQ-09固有4件は上記のとおり再実行した。

## PQ-10集中検証

```powershell
uv run --locked --extra dev pytest tests/validation/test_unit_invariance.py -q
uv run --locked --extra dev python -m tools.validation.unit_invariance --output tmp/product-quality-pq10-measurements.json
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq10-current.xml
```

- 5成功、失敗0、skip 0、終了コード0
- solver・診断を含む集中試験は104成功、失敗0、skip 0、終了コード0
- 全件は2,023成功、failure 0、error 0、skip 0、終了コード0
- JUnit実測1,222.748秒、pytest表示1,222.76秒（20分22秒）
- Python 3.13.11 / pytest 9.0.2 / NumPy 2.4.1 / SciPy 1.17.0
- Wiki 13ページ、Python 21ブロック、JSON 17ブロック、実行例18件が成功
- N–m、N–mm、kN–mの荷重制御・循環載荷・変位制御・強制変位・固有値を比較
- 材料骨格、断面、密度、荷重・モーメント、並進・回転ばね、曲率を一貫換算
- 詳細値は[PQ-10報告](../report/product-quality-pq10.md)と生成JSONに記録

## PQ-12集中検証

```powershell
$env:OMP_NUM_THREADS='1'
$env:OPENBLAS_NUM_THREADS='1'
$env:MKL_NUM_THREADS='1'
$env:NUMEXPR_NUM_THREADS='1'
uv run --locked --extra dev pytest tests/validation/test_performance.py -q
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq12-current.xml
```

- 性能契約3件、PQ-09／PQ-10を含む集中12件が成功
- 全件2,026成功、failure 0、error 0、skip 0、終了コード0
- JUnit実測1,265.754秒（21分06秒）
- Wiki 13ページ、Python 21 block、JSON 17 block、実行例18件が成功
- baselineは13 workload、warmup 1回、各5反復、4種のthread環境変数を1へ固定
- tool SHA-256、完全commit hash、dirty状態をbaseline provenanceへ保存

## 残る項目

- PQ-04: GitHubの`pypi` environmentとPyPI Trusted Publisherを設定し、commit／push後の
  workflow runと未使用versionのtag公開を確認する。ユーザー判断で保留中。
- PQ-11: GF-06の実験資料を受領後、入力同一性、単位、境界条件、材料較正、独立比較を進める。
- PQ-12後続候補: シェル後処理と材料非線形の反復組立をprofileし、正しさを維持できる変更だけを
  baselineと比較する。これはPQ-12完了条件ではなく、計測から得た継続改善候補である。

## 維持する品質ルール

- 期待値を製品ソルバーから生成しない。
- skip、比較項目削除、許容誤差の緩和で完了させない。
- 未対応と未検証を「対応済み」と表現しない。
- 参照更新では入力同一性、出典、前後ハッシュ、更新理由を残す。
- 数値共通処理またはリリース候補の変更後はclean環境で全件を実行する。
- 各PQの完了時にロードマップ、検証報告、ユーザー向け保証範囲を同時に更新する。
