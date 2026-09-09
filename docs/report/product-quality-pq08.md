# PQ-08 解析診断・結果メタデータ実装報告

実施日: 2026-09-09〜2026-09-10  
基準ブランチ／HEAD: `main` / `6085418c53f03f200c2c0a34609973ae021e3653`  
対象計画: [プロダクト品質の修正ロードマップ](../plans/product-quality-roadmap.md#pq-08-診断と結果メタデータ)

## 結論

PQ-08を完了した。Pythonの既存`ValueError`／`RuntimeError`契約を保ちながら、入力不正、
未対応解析、構造機構、数値悪条件、材料非線形／モーダル未収束を安定した`error_code`で
識別できるようにした。HTTPも同じ診断を固定形式のJSONで返す。

成功結果には再現条件と最終ソルバー状態を持つ`metadata`を追加した。モデルの座標・単位宣言と
結果メタデータはJSON保存往復で保持される。`src/fem`の無条件な標準出力はPython loggingへ移した。

## REDで確認した問題

- 静解析の特異剛性と通常の入力不正が同じ`ValueError`／HTTP `invalid_input`に混在した。
- 未対応組合せは要素IDを持っていてもHTTPで構造化されなかった。
- 非線形未収束以外の失敗はPythonから安定した機械コードを取得できなかった。
- 結果にschema、製品version、入力hash、単位、残差、反復、高精度経路がなかった。
- Newton反復、モデル分割、旧要素変換、要素警告が標準出力へ無条件に書かれた。

実装中の最初の全件実行は2,010成功・2失敗だった。失敗は物理応答ではなく、直接構築と
JSON構築で材料名、せん断補正、分割由来情報などが異なるのに、新しい`input_sha256`まで
同一とした既存経路同値試験である。hashから実入力差を消さず、数値結果の同値比較だけから
由来hashを除外した。解析条件表示は実際に使う非`None`項目へ正規化した。

## 実装した診断契約

| `error_code` | 区分 | Python互換基底 | HTTP |
|---|---|---|---:|
| `invalid_input` | 入力 | `ValueError` | 400 |
| `unsupported_analysis` | 未知解析／未対応組合せ | `ValueError` | 400／422 |
| `structural_mechanism` | 剛体運動・特異剛性 | `ValueError` | 422 |
| `numerical_ill_conditioning` | 数値ランク・釣合い精度 | `ValueError` | 422 |
| `nonlinear_nonconvergence` | 材料非線形未収束 | `RuntimeError` | 422 |
| `modal_nonconvergence` | 固有値ソルバー未収束 | `RuntimeError` | 422 |
| `analysis_failure` | 未分類の内部失敗 | 元例外 | 500 |

HTTPエラーは`error`、`error_code`、`error_category`、`converged: false`を必ず返す。
`details`には確定した情報だけを入れる。未対応機能は解析種別と要素ID付き`issues`、
ゼロ対角／ゼロ行は`matrix_dofs`、非線形未収束は`step`と`load_factor`を持つ。
一般的なLU特異性から原因節点・自由度を推測しない。

未分類の後処理`LinAlgError`は、Pythonでは元の型を保ちHTTPでは従来どおり500とした。
未知解析のHTTP 400も維持した。失敗した`FemModel.run()`は常に`results`を消去する。

## 結果メタデータ

成功結果の`metadata`は次を持つ。

- `schema_version: "1.0"`
- `product.name`と配布物に一致する`product.version`
- 実解析種別を含む正規化モデルJSONの`input_sha256`
- 実際に使う項目へ正規化した`analysis.type/parameters`
- `global_cartesian`の座標宣言
- SIへ決め打ちしない`consistent_user_defined`単位宣言
- 合計／段階別反復数、最終絶対／相対残差、構造化警告配列
- 線形梁の高精度経路を使ったかを示す`high_precision`

既定の長さ、力、質量、時間は`unspecified`で、自動変換しない。利用者は
`model.model_metadata["units"]`へ宣言でき、保存用モデルJSONの`model_metadata`で往復する。
結果JSONの`metadata`も通常のJSON値として保持する。

`input_sha256`は元ファイルの生バイトhashではない。ケース選択・分割後の解析モデルを対象とし、
同じモデルの保存往復では一致し、荷重変更では変化する。直接構築と旧編集JSON構築のように、
現在の応答が同じでも実入力表現が異なる経路ではhashが異なり得る。

## ログ契約

`src/fem`配下の`print()`をすべて除き、標準loggingへ移した。Newton反復は
`fem.equilibrium`のDEBUG、未収束通知とモデル変換はINFO／DEBUG、入力補正や数値警告は
該当モジュールのWARNINGで取得できる。ライブラリ自身はhandlerを強制しない。

旧CP932試験は、文字コード依存の標準出力を要求する契約から、標準出力が空で
`fem.model`のDEBUGログに同じV0互換情報がある契約へ変更した。

## 回帰試験

新規`tests/io/test_diagnostics.py`の9件で次を固定した。

- 静解析、材料非線形、固有値解析のメタデータ
- 高精度線形梁の`high_precision: true`
- モデル／結果保存往復、hashの保持と入力変更による変化
- NewtonログをDEBUGで取得でき、標準出力は空であること
- 未知解析と構造機構のPython／HTTP同一コード
- 確定できるゼロ対角自由度、数値悪条件、非線形失敗step
- 失敗時の古い結果消去

集中検証:

```powershell
uv run --locked --extra dev pytest tests/io tests/solvers tests/integration/test_model_contracts.py tests/integration/test_beam_precision.py -q
uv run --locked --extra dev python -m tools.validation.check_wiki
```

- 関連pytest: 205成功、失敗0
- Wiki: 13ページ、Python 21ブロック、JSON 17ブロック、ローカルリンクを検査
- 実行例: 18成功
- `rg -n "print\\(" src/fem --glob "*.py"`: 該当0件

## 全件検証

```powershell
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq08-current.xml
```

- Python 3.13.11 / pytest 9.0.2
- 2,012成功、failure 0、error 0、skip 0、終了コード0
- JUnit実測1,090.000秒、pytest表示1,090.01秒（18分10秒）

## 互換性と残る制約

- 成功結果に`metadata`が追加される。固定キー集合を厳密比較する利用者は新キーを許容する必要がある。
- 既存の`ValueError`／`RuntimeError`捕捉は継続できる。詳細型と`error_code`は追加契約である。
- モデル保存JSONに`model_metadata`が追加される。旧入力はそのまま読め、単位未指定として扱う。
- 既存結果JSONはそのまま読めるが、過去結果へメタデータを推測して補完しない。
- `solver.warnings`は構造化して結果へ付記した警告であり、全loggingレコードの自動転記ではない。
- 一般的な特異行列について原因節点・要素を自動同定する機能はない。
- 単位変換による非線形収束判定の不変性はPQ-10で検証する。
