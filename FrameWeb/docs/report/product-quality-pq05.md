# PQ-05 導入文書・品質説明の検証報告

実施日: 2026-09-09  
実装開始時HEAD: `aedbd26b62607e11b2e80744e449518da567db0e`

## 結論

PQ-05を完了した。ルートREADMEを配布後に実行できる導入・解析例へ置き換え、
現行実装にない上限・同時実行・精度保証、存在しないテストコマンド、内部配置名のimport、
文字化けした末尾、根拠のない成功表現を削除した。

## READMEの保証範囲

- 公開importは`fem`と`app`であり、`src.fem`を案内しない。
- Python 3.11以上、PyPI wheel利用、uvによる開発環境を分けて説明する。
- N・m・sの一貫単位系による2節点軸材を使い、独立解`u=F L/(E A)=0.05`を示す。
- 線形静解析、材料非線形解析、固有値解析と主要要素を示す。
- 荷重ケース、一次wedge固有値解析、未提供の解析種別などの制約を明記する。
- wheelは`fem`・`app`、HTTPの`main.py`はリポジトリ／コンテナという配布境界を示す。
- 全件pytestとWiki実行例の実在するコマンドを示す。
- モデル妥当性には支持・単位・メッシュと独立比較が必要であることを明記する。

## リンクとwheelの検証

2026-09-09に次のHTTP状態を確認した。

- `https://sasaco.github.io/fempy/`: 200。
- 旧`https://structuralengine.github.io/FEMPython/`: 404。READMEから除去した。

README内の全ローカルリンクは存在する。最終wheelのMETADATAを展開し、次を確認した。

- `Version: 1.0.2`。
- `Requires-Python: >=3.11`。
- README本文に`from src.fem`と旧404 URLがない。
- 公開ユーザーガイドURLが含まれる。

最終wheel SHA-256:
`8dd12585bcadb3ff905bf6bd96954ece1483163b9f0bd07c012d2779845682a9`

このwheelをPython 3.13.11の隔離venvへ再導入し、`python -I`で`fem`・`app` import、
配布・実行時version一致、READMEと同じ軸材の変位0.05、モデル・結果JSON保存を確認した。

## Wiki検証

```powershell
uv run --locked --extra dev python -m tools.validation.check_wiki
```

13ページ、Python 18ブロック、JSON 17ブロック、実行例18件が成功した。
READMEの導入例とWikiの利用契約は、公開import、単位、wheel／HTTP境界で一致する。

文字化けしていた`docs/plans/test_plan.md`は、現行の項目表・台帳を正本として参照し、
PQ-06〜12の未検証範囲、テスト層、変更ゲート、標準コマンドを示す文書へ置き換えた。

最終的に、`aedbd26b62607e11b2e80744e449518da567db0e`へ本ロードマップの変更だけを重ねた
clean worktreeで全1,972件が成功した（失敗0、skip 0、終了コード0、1,134.23秒）。
