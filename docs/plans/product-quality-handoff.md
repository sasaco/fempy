# プロダクト品質ロードマップ引継ぎ

更新日: 2026-09-10

対象計画: [プロダクト品質の修正ロードマップ](product-quality-roadmap.md)

## 結論

PQ-00〜03・PQ-05〜08は完了した。PQ-04はユーザー判断で保留し、段階2は未完了のまま維持する。
段階3「利用時の信頼性」では、機械可読な機能対応表、未対応組合せの解析前拒否、
検証済みLegacy VTK出力、安定した解析診断と結果メタデータを実装した。次はPQ-09の
メッシュ誤差と適用範囲へ進む。

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

PQ-06開始時は`main`の`6085418c53f03f200c2c0a34609973ae021e3653`で、作業ツリーはcleanだった。
以前のPQ-07変更はこのコミットに含まれている。今回のPQ-06変更はVTKライター／変換、独立reader試験、
開発依存、Wiki、ロードマップ／引継ぎ／報告に限定した。続けてPQ-08の診断、メタデータ、
logging、試験、利用者文書を同じ作業ツリーへ追加した。commit、pushは行っていない。
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

## 次項目: PQ-09

PQ-04は保留のまま、段階4のPQ-09を実施する。梁・板・ソリッドの非一様変形について、
複数メッシュで変位・応力・エネルギー誤差と収束率を測り、歪み・アスペクト比・板厚・
ポアソン比の適用限界を、独立参照解とともに報告する。

## 維持する品質ルール

- 期待値を製品ソルバーから生成しない。
- skip、比較項目削除、許容誤差の緩和で完了させない。
- 未対応と未検証を「対応済み」と表現しない。
- 参照更新では入力同一性、出典、前後ハッシュ、更新理由を残す。
- 数値共通処理またはリリース候補の変更後はclean環境で全件を実行する。
- 各PQの完了時にロードマップ、検証報告、ユーザー向け保証範囲を同時に更新する。
