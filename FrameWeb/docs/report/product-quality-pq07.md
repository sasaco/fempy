# PQ-07 機能対応表・解析前検証の実装報告

実施日: 2026-09-09<br>
基準コミット: `a4392b9`<br>
対象: 機械可読な機能対応表、公開解析入口の事前拒否、README／Wiki同期

## 結論

PQ-07を完了した。配布wheelに含まれる`src/fem/capabilities.json`を機能対応の正本とし、
要素の公開名・旧名、節点数、実際の生成クラス、解析種別、質量行列、荷重、結果出力を
`verified`、`implemented`、`unsupported`の3状態で登録した。

`FemModel.run()`は、要素を生成した後、剛性・質量行列や荷重ベクトルを組み立てる前に正本を検査する。
未対応の組合せは`UnsupportedCapabilityError`（`ValueError`互換）として、解析種別、要素ID、
正規化した要素型、機能、理由を返す。再解析時の`results`は従来どおり先に消去されるため、
事前拒否後に古い成功結果は残らない。

## 正本と公開API

- `src/fem/capabilities.json`: schema version 1のJSON正本。11要素型、3解析種別、
  8荷重種別、6結果種別を登録する。
- `src/fem/capabilities.py`: schemaの完全性、状態値、alias重複を起動時に検査する。
- `get_capability_registry()`: 呼出側で変更しても内部キャッシュを汚さないコピーを返す。
- `get_element_capability()`／`canonical_element_type()`: 現行名と旧V0名を同じ定義で解決する。
- `UnsupportedCapabilityError.issues`: 要素ごとの機械可読な拒否内容を保持する。

`verified`は公開`FemModel`経路に対象を絞った数値または契約検証がある状態、`implemented`は
処理経路はあるが、その組合せ固有の力学検証が未完了の状態、`unsupported`は解析前に拒否する状態である。
実装の存在を検証済みと読み替えない。

## 解析前に拒否する組合せ

- 一次`wedge`の`modal`: 公開経路が生成する
  `fem.elements.advanced_element.WedgeElement`には質量行列がない。
- `pyramid`の全解析: 剛性行列が未実装である。
- `hexa20`の全解析: 剛性行列が未実装である。実装済みの20節点六面体名は`hexa2`である。
- `tetra`、`wedge`、`hexa`および二次ソリッドへのシェル面圧: ソリッド面圧は未実装である。

一次wedgeでは同名の`fem.elements.solid_element.WedgeElement`に質量行列実装があっても、
公開`FemModel`が生成しない別クラスなので対応根拠にしない。事前検証は実インスタンスの完全修飾クラス名を
正本と照合し、別実装へすり替わった場合はレジストリ不整合として失敗する。

`pyramid`と`hexa20`は従来、要素追加時に`set_material_properties`がないという無関係な
`AttributeError`で停止していた。未対応stubも構築だけは完了させ、解析時に要素IDと本来の未実装理由を
返すようにした。剛性・質量の未実装処理を呼んで成功扱いにする変更ではない。

## README／Wikiの同期

`tools.validation.render_capabilities`がJSON正本から同じMarkdown表をREADMEと
`docs/wiki/elements.md`へ生成する。表は解析状態、質量行列、利用できる荷重、取得できる結果を示す。
`tools.validation.check_wiki`は生成差分も検査するため、JSONだけを変更して利用者向け表を更新し忘れると失敗する。

更新方法:

```powershell
uv run --locked --extra dev python -m tools.validation.render_capabilities --write
uv run --locked --extra dev python -m tools.validation.render_capabilities
```

## 検証結果

集中試験:

```powershell
uv run --locked --extra dev pytest tests/solvers/test_capabilities.py tests/solvers/test_modal_analysis.py tests/io/test_pressure.py tests/io/test_http.py -q
```

- 59成功、失敗0（PQ-07試験追加前後を含む集中実行）
- 最終PQ-07専用試験は14成功
- 一次wedgeの現行名／旧名、複数要素ID、保存用JSON入力、pyramid、hexa20、
  ソリッド面圧、同名別実装の誤認、文書同期を検証した。

関連回帰:

```powershell
uv run --locked --extra dev pytest tests/solvers tests/integration tests/io tests/postprocess -q
uv run --locked --extra dev python -m tools.validation.check_wiki
```

- 解析・入出力・後処理816成功、失敗0
- Wiki 13ページ、Python 19ブロック、JSON 17ブロック、実行例18件成功
- `compileall`成功

配布物:

- `uv build --wheel --out-dir tmp/pq07-dist`成功
- wheelに`fem/capabilities.json`が含まれることをZIP内容から確認
- Python 3.13.11の`--seed`付き隔離venvでwheelを導入し、公開解析例に成功
- 隔離インタープリタから11要素型の機能表を取得し、一次wedgeの`modal=unsupported`を確認

全件試験:

```powershell
uv run --locked --extra dev pytest --junitxml=tmp/product-quality-pq07.xml
```

- Python 3.13.11 / pytest 9.0.2
- 1,996成功、失敗0、skip 0、終了コード0
- 1,706.39秒（28分26秒）
- JUnit: `tmp/product-quality-pq07.xml`

## 残る範囲

`implemented`とした組合せは未対応ではないが、個別の力学検証を追加するまで`verified`へ昇格しない。
荷重の一般的な解析種別別診断、HTTPの専用エラーコード、結果メタデータはPQ-08で扱う。
VTKセル型・値対応は機能表の結果保証とは分離し、次のPQ-06で修正・読戻し検証する。

PQ-04はユーザー判断により保留であり、段階2は未完了のまま維持する。
