# 軸力変動・I更新の検証サンプル

追加した2ファイルは公開の旧形式JSONとして読み込める。単位は kN・m・rad、値は実装検証用の仮定値である。
いずれも長さ1 mの片持ち梁（固定端10、先端30、部材7）を用いる。局所xは全体x、曲げは `moment_z`。
骨格は `M_b(φ,Nd) = (1 + Nd / 1 kN) m_0(φ)`、`Nd=-N`（圧縮正）とする。

| ファイル | 確認する機能 |
|---|---|
| [axial_force_shear_load_control.json](axial_force_shear_load_control.json) | 非線形軸方向則の折れ点を跨ぐNd経路、有限Gを使うせん断縮約、4荷重段階の累積せん断力と変位 |
| [axial_force_shear_displacement_control.json](axial_force_shear_displacement_control.json) | Nd依存骨格のK1～K4、負のI・C、荷重極大点以降のNd減少、第4参照点以降の延長、10変位目標 |

## 荷重制御

先端荷重は `(Fx,Fy,Mz)=λ(-0.6,0.12,0.02)`、`λ=.25,.5,.75,1`。
釣合いから `Nd=.6λ`、`V=.12λ`、中央 `M=.08λ` となる。
軸方向則は `|ε|=.0004, N=.4` で初期剛性1000から500へ変わる。
第3段階はこの折れ点を跨ぐため、曲げ面へ渡すNd経路を軸ひずみの同じ経路比で二分する必要がある。
最終値は `ux=-.0008 m, φ=.0005 rad/m, I=.16 m⁴, uy=.0006881527148820168 m`。

`E=1000, G=400, A=1, k=5/6` を明示してせん断変形を有効にした。
旧形式JSONではG省略時にせん断変形が無効になるため、Gも保存する。
基準幾何Iは `.1 m⁴`、履歴接線に基づく有効IはNdとともに `.1→.16 m⁴` に変わる。

## 変位制御

先端荷重パターンは `(Fx,Fy,Mz)=λ(-.2,.02,.09)`、先端 `rz=φ` を指定する。
中央の釣合いは `M=.1λ`。よって骨格上では独立に

```text
λ = m_0(φ) / (.1 - .2 m_0(φ)),  Nd=.2λ,  V=.02λ
```

と求まる。`m_0` の点は `(φ,M)=(.001,.1),(.003,.2),(.005,.25),(.007,.2)`。
`φ=.005` で `λ=5, Nd=1` に達し、その後のK4=-25で荷重とNdが減少する。
最終 `φ=.008` では `λ=35/13, Nd=7/13, M=7/26, V=7/130`、
`I=-.03846153846153846 m⁴, uy=.00421456884390792 m`。
`shear_correction=false` とし、`C=12B/L²` による積分を検証する。
曲率は単調増加であり、曲げの除荷・内部ループを検証するサンプルではない。

## 独立期待値と実行

期待値は [独立参照式](../../support/oracles/axial_force_samples.py) に実装した釣合いと区分積分から作成した。
製品の補間器、履歴則、ソルバーから期待値を生成していない。
固定された各経路区間で `B=k(1+Nd)` とし、有限GAの場合は
`C=GA-GA²/(GA+12k(1+Nd))` を対数の原始関数で積分する。
せん断変形を無視する場合は線形関数 `C=12k(1+Nd)` の積分になる。
各荷重段階で `Δs=ΔV/平均C` を累積し、`uy=s+φ/2` として変位を求める。
終端Cを全せん断変形へ掛け直す計算とは結果が異なる。

保存 `result` は既存の断面切断面符号に従う。荷重制御は最終結果、変位制御は全10段階を保存する。
回帰試験では両方の全段階について、変位・反力・部材端力・曲率・Nd・I・C・累積Vを独立値と比較する。
正規化JSONへ保存して再解析する試験も含む。manifestには入力のUTF-8/LF SHA256と契約を登録した。

```powershell
uv run --locked --extra dev pytest -q -W error tests/regression/test_axial_force_samples.py
uv run --locked --extra dev python -m tools.debug.execute_model tests/data/snap/axial_force_shear_load_control.json --output-dir tmp/axial-force-load
uv run --locked --extra dev python -m tools.debug.execute_model tests/data/snap/axial_force_shear_displacement_control.json --output-dir tmp/axial-force-displacement
```

この2例の成功だけで、任意のNd履歴や局所特異化を含む全機能の妥当性は判定しない。
レビューで見つけた不具合2件は修正済み。再現条件は [レビュー記録](../../../docs/report/軸力変動実装レビュー.md)、
一定Ndの折れ点接線、移動再載荷、内部特異点と公開ソルバーのrollbackの追加検証は
[実装検証記録](../../../docs/report/軸力変動実装検証.md)を参照する。
