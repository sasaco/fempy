# PQ-04 CI・PyPI公開経路報告

実施日: 2026-09-09
実装開始時HEAD: `6ccbe062e5ff055c50d0e8890bc8707e4624c705`

## 結論

PQ-04のworkflow実装とローカル検証を完了した。通常CIと公開CIは同じ再利用可能な
検証workflowを使い、公開候補のwheelとsdistを一度だけビルドする。以後のjobは同じ
GitHub artifactを取得し、commitと配布物SHA-256を再照合する。公開jobはすべての検証jobが
成功しない限り開始せず、公開直前の再ビルドを行わない。

実際のGitHub Actions実行、commit、push、タグ作成、PyPI公開は本作業に含めていない。
GitHub APIで確認できたenvironmentは`github-pages`だけで、`pypi`はまだない。
そのためPQ-04全体は外部接続確認待ちであり、完了とはしない。

作業中に別セッションが先行品質変更`2c60d45`と理論マニュアル移動`2d80748`をcommit・pushし、
HEAD／`origin/main`は`2d80748906a5307526abd701aafacc52cc9e8935`へ進んだ。本作業はそれらを
変更せず、最終的にPQ-04の8ファイルだけを未コミット差分として残した。

## 検証workflow

`.github/workflows/tests.yml`は`main`へのpush／pull request、手動実行、`workflow_call`に対応する。

- Ubuntu / Python 3.13.11 / uv 0.10.0でwheelとsdistを一度だけ作る。
- `release-manifest.json`へcommit、version、ファイル名、サイズ、SHA-256を保存する。
- Windows / Python 3.13.11 / Node.js 24で材料非線形集合、全件pytest、Wiki例を実行し、
  材料非線形と全件のJUnitをartifactとして残す。メタデータ試験は全件に含まれる。
- 同じ配布artifactをUbuntuのPython 3.11、3.12、3.13とWindowsのPython 3.13へ導入する。
- 各行列jobはmanifestとcommitを再照合し、`python -I`、一時作業ディレクトリから
  `fem`／`app` import、version、独立解0.05の静解析、モデル・結果保存、`main`非同梱を検査する。
- 外部Actionsは2026-09-09時点の公式tag／branchのcommit SHAへ固定した。

manifest検査の回帰試験は、commitの変更とwheel byteの改変をそれぞれ拒否する。

## 公開workflow

`.github/workflows/publish-pypi.yml`は`v[0-9]*`タグだけで発火し、最初のjobとして上記workflowを
呼び出す。公開jobはその成功を`needs`で要求し、返されたartifactだけをダウンロードする。

- タグが正確に`v<配布version>`であることを検査する。
- manifestのcommit、version、wheelファイル名、SHA-256と実ファイルを再照合する。
- PyPIに同一versionがあれば、上書きや`skip-existing`をせず明示的に失敗する。
- API token secretを使わず、GitHubの`pypi` environmentとPyPI Trusted Publishingを使う。
- `id-token: write`は、ソースのcheckoutやビルドを行わない公開jobだけへ付与する。

PyPI側ではTrusted Publisherにrepository `sasaco/fempy`、workflow
`publish-pypi.yml`、environment `pypi`を登録し、GitHub側のenvironment保護規則を設定する必要がある。

## 重複versionの確認

2026-09-09時点でPyPIには`FEMPython 1.0.2`が存在する。

| 配布物 | PyPI公開済みSHA-256 | 現行検証SHA-256 |
|---|---|---|
| `fempython-1.0.2-py3-none-any.whl` | `91df6e78f893ca0dce39da292a23e677e7154df432a7f6007dc9189a8bd0d362` | `8dd12585bcadb3ff905bf6bd96954ece1483163b9f0bd07c012d2779845682a9` |
| `fempython-1.0.2.tar.gz` | `8f7989db849e6008e15d51edb068380e541590f506667a91ed0f0916a010f0da` | `f1250d892e7cc9e0a5397b40794c73b99500fca0958c4c0f374175dc0965c032` |

PyPIの既存ファイルは置換できず、同じversionの異なるbyte列を正しい成果物として扱えない。
実リリース時は単一version源を1.0.3以上の未使用versionへ更新したcommitを、同じCIで最初から検証する。

## ローカル検証

```powershell
uv lock --check
uv build --out-dir <一時ディレクトリ>/packages
uv run --locked --extra dev python tools/validation/check_distribution.py `
  --packages-dir <一時ディレクトリ>/packages `
  --write-manifest <一時ディレクトリ>/release-manifest.json --commit LOCAL
uv run --locked --extra dev pytest `
  tests/harness/test_release_workflows.py tests/io/test_package_metadata.py -q
actionlint .github/workflows/tests.yml .github/workflows/publish-pypi.yml
```

- `uv lock --check`: 成功。
- wheel／sdistビルド、metadata・同梱境界、manifest生成と再照合: 成功。
- wheel SHA-256はPQ-03／PQ-05の最終wheelと一致した。
- 新規Python 3.13.11 venvでの隔離wheel導入・解析・保存: 成功。
- workflow契約、manifestのcommit／byte改変検出、package metadata: 5成功。
- actionlint 1.7.12: 診断0、終了コード0。
- 全件pytest: 1,982成功、失敗0、skip 0、終了コード0、1,078.43秒（17分58秒）。
- 材料非線形: 902成功、1,080対象外、終了コード0、44.39秒。
- JUnit: `tmp/product-quality-pq04-current.xml`、`tmp/product-quality-pq04-material.xml`。

GitHub上の行列実行結果は、実装をcommitしてpushした後に確認する。実公開では未使用version、
必須jobの成功、artifactのmanifest、PyPI画面のSHA-256を改めて照合する。
