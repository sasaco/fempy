# プロダクト品質ロードマップ引継ぎ

更新日: 2026-09-09

対象計画: [プロダクト品質の修正ロードマップ](product-quality-roadmap.md)

## 結論

PQ-00〜03とPQ-05は完了した。PQ-04では同じコミットから一度だけ作ったartifactを、全件pytest、
材料非線形、Wiki例、隔離導入で検証してからTrusted Publishingへ渡すCIを実装・ローカル検証した。
GitHub／PyPIの外部設定とリモート実行確認が残る。そこまで確認後、PQ-07の機械可読な対応表と
解析前拒否、続いてPQ-06/PQ-08のVTK・診断へ進む。

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

PQ-04開始時のHEADは`6ccbe062e5ff055c50d0e8890bc8707e4624c705`だった。作業中に別セッションが
先行する品質変更を`2c60d45`、理論マニュアル移動を`2d80748`としてcommit・pushし、
現在のHEAD／`origin/main`は`2d80748906a5307526abd701aafacc52cc9e8935`へ進んだ。
これらのcommitは本作業で作成・変更していない。

最終確認時の未コミット変更は、下記PQ-04のworkflow 2件、検証ツール2件、試験1件、
ロードマップ／引継ぎ／PQ-04報告の計8ファイルだけである。次回開始時も`git status --short`と
`git diff`を取り直し、他セッションが追加した変更をstash、reset、整形、commitしない。

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

## 外部接続確認後の次項目: PQ-07

PQ-04の次はPQ-07を推奨する。一次wedge固有値解析などの未対応組合せを、要素IDと
理由を含む解析前エラーとして返し、README・Wikiの対応表と同じ機械可読定義から生成する。

## 維持する品質ルール

- 期待値を製品ソルバーから生成しない。
- skip、比較項目削除、許容誤差の緩和で完了させない。
- 未対応と未検証を「対応済み」と表現しない。
- 参照更新では入力同一性、出典、前後ハッシュ、更新理由を残す。
- 数値共通処理またはリリース候補の変更後はclean環境で全件を実行する。
- 各PQの完了時にロードマップ、検証報告、ユーザー向け保証範囲を同時に更新する。
