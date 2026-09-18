## 1. Scope

用語と包含関係は概ね正しいです。

- `AnalysisResult`: 1解析ケースのcanonical result。
- `AnalysisStepResult`: 非線形解析内の受理済みstep。
- `AnalysisResultSet`: 複数ケースのordered collection。旧UI形式ではありません。
- `FrameResultSet`: FrameWebforJS表示用の投影。

ただし「`FrameResultSet`は`AnalysisResultSet`を投影したもの」という表現は修正が必要です。投影には解析結果だけでなく、解いたモデル、節点ラベル、部材分割、元入力が必要です。[legacy_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_results.py:96)

正確には、両者は同じ内部case solutionから生成される兄弟representationです。

## 2. Complexity Classification

**MODERATE**

solverアルゴリズムの変更は不要ですが、次の境界を同時に守る必要があります。

- Python内部結果
- HTTP media typeとwire schema
- Angular worker入力
- 保存済みraw case map
- `legacy-cases-v1`の完全互換性

想定範囲は製品・テスト・文書を合わせて約10～14ファイルです。

## 3. Integration Points

- [main.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:105): `Accept`による表現選択。現在はsingle caseと全caseで計算量も変わります。
- [legacy_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_results.py:35): case列挙、fresh model、解析、Frame投影が一体化しています。
- [solver_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/solver_results.py:144): `AnalysisStepResult`の事実上の生成元。
- [solver_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/solver_results.py:191): 最終状態と非線形履歴をまとめる`AnalysisResult`境界。
- [app.component.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/app.component.ts:252): 現在の`legacy-cases-v1`要求元。
- [result-data.service.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/result-data.service.ts:49): wire map検証とworker入力境界。
- [menu.component.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/components/menu/menu.component.ts:417): 保存済みraw case mapの読込境界。

## 4. Affected Files

| 区分 | 対象 |
|---|---|
| Backend既存 | `main.py`, `legacy_results.py` |
| Backend新規候補 | `result_contracts.py`, `result_sets.py`, `frame_results.py` |
| Frontend | `app.component.ts`, `result-data.service.ts/.spec.ts`, 新規`result-contracts.ts` |
| Tests | `test_legacy_cases_api.py`維持、新規`test_result_set_api.py`、default HTTP/nonlinear履歴テスト |
| Docs | `endpoints.md`, `results.md`, `DESIGN.md` |

`model.py`、`solver_results.py`、`legacy_beam.py`は基本的に利用側であり、最初の段階では変更対象にしない方が安全です。

## 5. Risks and Edge Cases

1. **メモリ使用量**  
   最大256件の`FemModel`を保持するmaterialized `CaseSolutionSet`は避けるべきです。現実装はcaseごとに投影後、modelを破棄できます。非線形履歴を含むcanonical resultも大きいため、256件上限だけでは安全性を保証しません。

2. **rateの意味**  
   canonical `AnalysisResultSet`には適用せず、`FrameResultSet`とlegacy aliasだけに後乗算します。現在は不正・非有限rateを黙って`1.0`へ変換しています。[legacy_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_results.py:88)  
   この挙動をv1で維持するか拒否するか、実装前に固定が必要です。

3. **順序**  
   新契約はobject key順ではなく`cases` arrayを使う必要があります。特にJavaScriptの整数風keyは挿入順と異なる列挙になり得ます。

4. **result variant**  
   static、material nonlinear、modalを同一必須field集合で検証できません。`analysis_type`によるdiscriminated validationが必要です。Frame投影v1はmodalを明示的に拒否します。

5. **Accept negotiation**  
   新しいmedia typeを増やす前に、未知version、`q=0`、複数のFrameWeb vendor type指定を決定論的に処理する必要があります。

6. **frontend互換性**  
   現在のvalidatorは`disg/reac/fsec`だけを必須とし、`shell_fsec/size`は検証していません。[result-data.service.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/result-data.service.ts:76)  
   新wire契約は5項目を厳格検証し、保存済みlegacy mapには別の寛容なnormalizerを残すべきです。

7. **対応範囲の過大表示**  
   現multi-case処理はlegacy beam専用で、modern input、shell、solidを拒否します。[legacy_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_results.py:55) `FrameResultSet v1`もこの制限を明記する必要があります。

## 6. Recommended Architecture

別endpointは正しさのためには必要ありません。既存のfunctions-framework配置と互換性を考えると、最小変更は`POST /`と明示的な`Accept`を維持する構成です。別endpointは将来のv2候補とします。

内部では、全modelを保持する`CaseSolutionSet`ではなく、一時的な`CaseSolution`を表現別projectorへ渡します。

```text
validate/enumerate cases
  -> caseごとにfresh FemModelでsolve
  -> ephemeral CaseSolution(case_id, result, model, rate)
  -> requested representationへ即時変換
  -> modelを破棄
  -> 全case成功後だけresponseを返す
```

推奨wire schemaは次です。

```json
{
  "kind": "analysis_result_set",
  "schema_version": "1.0",
  "cases": [
    {"case_id": "2", "result": {"analysis_type": "static"}}
  ]
}
```

```json
{
  "kind": "frame_result_set",
  "schema_version": "1.0",
  "cases": [
    {
      "case_id": "2",
      "result": {
        "disg": {},
        "reac": {},
        "fsec": {},
        "shell_fsec": {},
        "size": 42
      }
    }
  ]
}
```

`case_count`は`cases.length`と重複するため不要です。`legacy-cases-v1`だけは従来のbare mapを完全維持します。

実装順は以下が最小です。

1. case実行とFrame投影を分離。既存legacy responseが完全一致することを確認。
2. `AnalysisResultSet`と`FrameResultSet`のmedia type・envelopeを追加。
3. frontendに両形式のnormalizerを追加し、HTTPだけ`frame-result-set-v1`へ移行。
4. 保存済みraw map、旧alias、default single-caseを回帰確認。

## 7. Explicit Non-Goals

- FEM solver、数値結果、収束処理の変更
- `rate`を解析荷重係数へ再定義すること
- modern input、shell、solidのmulti-case対応
- 非線形stepをcaseへ展開すること
- 保存済みresult fileの強制移行
- worker完了管理全体の再設計
- 圧縮transportやC#印刷契約の変更
- `legacy-cases-v1`の即時削除
- 新endpointの追加

## 8. Verdict

**NEEDS_REVISION**

方向性と用語は妥当です。ただし実装開始前に、次の3点を設計へ反映してください。

1. `FrameResultSet`をwire `AnalysisResultSet`の直接変換ではなく、内部case solutionからの兄弟projectionと定義する。
2. 全`FemModel`を保持するmaterialized `CaseSolutionSet`を避ける。
3. wire envelope、Accept競合規則、rateの不正値処理、beam-only範囲を確定する。

これらを直せば、上記3段階で進行可能です。リポジトリへの変更は行っていません。
