# プロダクト品質ロードマップ引継ぎ

更新日: 2026-09-09

対象計画: [プロダクト品質の修正ロードマップ](product-quality-roadmap.md)

## 結論

PQ-00〜03とPQ-05は完了した。次セッションはPQ-04を最優先とし、同じコミットから作った
同じwheelを全件pytest、Wiki例、隔離導入で検証してから公開できるCIへ変更する。
その後はPQ-07の機械可読な対応表と解析前拒否、PQ-06/PQ-08のVTK・診断へ進む。

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

今回の品質変更以外に、変位制御サンプル関連の未コミット変更が並行して存在する。
少なくとも`tests/README.md`、`tests/data/manifest.json`、`tests/harness/test_suite_contracts.py`、
`tests/integration/test_input_routes.py`、`tests/support/sample_runner.py`、`tests/support/samples.py`、
`tests/data/snap/jr_k4_displacement_control.json`、
`tests/regression/test_displacement_control_history.py`、
`tests/support/oracles/jr_k4_bending.py`、
`docs/plans/jr-k4-displacement-control-sample-handoff.md`は別作業として保持する。

次回開始時は`git status --short`と`git diff`を取り直す。別作業をstash、reset、整形、
コミットしない。HEADが進んでいる可能性もあるため、この一覧だけで所有権を判断しない。

## 次セッションの最優先: PQ-04

対象:

- `.github/workflows/tests.yml`
- `.github/workflows/publish-pypi.yml`
- `tools.validation.check_wiki`
- wheel隔離導入試験

完了条件:

1. 全件pytest、Wiki例、wheel隔離導入、メタデータ試験のいずれかが失敗したら公開しない。
2. 検証jobが作ったwheelをartifactとして公開jobへ渡し、公開直前に再ビルドしない。
3. 検証対象コミット、wheelファイル名、SHA-256をCI成果物へ残す。
4. リリース発火条件と同一versionが既に存在する場合の挙動を明記する。
5. workflow変更の検証と、実PyPI公開を分ける。

PQ-04の次はPQ-07を推奨する。一次wedge固有値解析などの未対応組合せを、要素IDと
理由を含む解析前エラーとして返し、README・Wikiの対応表と同じ機械可読定義から生成する。

## 維持する品質ルール

- 期待値を製品ソルバーから生成しない。
- skip、比較項目削除、許容誤差の緩和で完了させない。
- 未対応と未検証を「対応済み」と表現しない。
- 参照更新では入力同一性、出典、前後ハッシュ、更新理由を残す。
- 数値共通処理またはリリース候補の変更後はclean環境で全件を実行する。
- 各PQの完了時にロードマップ、検証報告、ユーザー向け保証範囲を同時に更新する。
