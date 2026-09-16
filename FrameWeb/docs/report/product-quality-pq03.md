# PQ-03 パッケージ・バージョン実装報告

実施日: 2026-09-09  
実装開始時HEAD: `aedbd26b62607e11b2e80744e449518da567db0e`

## 結論

PQ-03を完了した。配布メタデータと実行時バージョンを1.0.2へ統一し、
新規の隔離環境へwheelを導入して公開import、モデル作成、解析、保存を確認した。

## 定義の一元化

- `src/fem/_version.py`の`__version__ = "1.0.2"`を単一の正本とした。
- `pyproject.toml`はPEP 621の動的versionとHatchのfile sourceで同じ値を読む。
- `fem.__version__`は同じ変数を公開する。
- Python 3.6〜3.10を宣言していた重複`setup.py`は廃止した。
- 対応Pythonは3.11以上とし、メタデータへ3.11、3.12、3.13を明記した。
- `requirements.txt`は依存を再列挙せず、`-e .[dev]`で`pyproject.toml`へ委譲する。

公開wheelは`fem`と`app`を含む。`main.py`はwheelに含めず、HTTPサーバーは
リポジトリまたはコンテナから起動する境界を維持した。functions-frameworkで過去に使われた
`main.FrameWeb3`は、現行`main.FEMPython`と同一関数を指す互換aliasとして残した。

## 隔離wheel検証

```powershell
uv lock --check
uv build --wheel --out-dir tmp/pq03-dist
uv venv <新規一時ディレクトリ>/.venv
uv pip install --python <新規venvのpython> tmp/pq03-dist/fempython-1.0.2-py3-none-any.whl
<新規venvのpython> -I -
```

Python 3.13.11の空venvへwheelと依存28件を導入した。`-I`でリポジトリを
Python検索パスに含めない状態にし、次を確認した。

- `fem`と`app`はvenvの`site-packages`からimportされた。
- `importlib.metadata.version("FEMPython") == fem.__version__ == "1.0.2"`。
- 2節点軸材の静解析で独立解`u=F L/(E A)=0.05`と一致した。
- `save_model()`と`save_results()`がJSONを生成し、保存結果の変位も0.05だった。
- wheelから`main`は見つからず、宣言したHTTP配布境界と一致した。

wheel SHA-256:
`8dd12585bcadb3ff905bf6bd96954ece1483163b9f0bd07c012d2779845682a9`

## 集中試験

```powershell
uv run --locked --extra dev pytest tests/io/test_package_metadata.py tests/io/test_http.py -q
```

23成功、失敗0、skip 0、終了コード0、6.17秒だった。テストは動的メタデータ、
実行時バージョン、旧HTTP関数名の互換aliasを固定する。

Linuxでの動作はwheelの`py3-none-any`という形式だけから保証しない。現在の必須CIはWindowsであり、
追加OSを正式に保証する場合はPQ-04で同じwheelのCI行列を追加する。

最終的に、`aedbd26b62607e11b2e80744e449518da567db0e`へ本ロードマップの変更だけを重ねた
clean worktreeで全1,972件が成功した（失敗0、skip 0、終了コード0、1,134.23秒）。
