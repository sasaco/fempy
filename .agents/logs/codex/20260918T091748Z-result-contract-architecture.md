結論は **APPROVED** です。複雑度は **MODERATE**。solver の数値処理を変更せず、ケース実行・canonical result・表示用 projection・wire negotiation を分離できます。設計のみ実施し、リポジトリは変更していません。

## 1. Architecture Summary

```text
POST /
  ├─ Accept negotiation
  ├─ default
  │    └─ existing single-case AnalysisResult
  └─ multi-case
       └─ validate legacy-beam case set
            └─ caseごとに fresh FemModel
                 └─ ephemeral CaseSolution
                      ├─ AnalysisResult accumulator
                      ├─ FrameCaseResult accumulator
                      └─ legacy bare-map accumulator
```

重要な原則は以下です。

- `AnalysisResultSet` と `FrameResultSet` は、wire payload 間の変換関係ではなく、同じ内部 `CaseSolution` から生成する兄弟表現。
- `FemModel` を含む `CaseSolutionSet` は作らない。
- 各ケースの model は投影後に解放する。
- レスポンスは全ケース成功後に一度だけ返す。
- nonlinear の `step_results` はケースの内側に残し、ケースとして展開しない。

現状はケース実行と表示投影が [legacy_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_results.py:35) に結合されています。これを分離します。

## 2. Backend Components and APIs

| Module | Responsibility / API |
|---|---|
| `result_contracts.py` | media type定数、`ResultRepresentation`、TypedDict、variant validation |
| `result_sets.py` | legacy-beam case列挙、256件制限、fresh model実行、canonical set構築 |
| `frame_results.py` | `FrameCaseResult`投影、Frame envelope、legacy map構築 |
| `legacy_results.py` | 既存`solve_legacy_cases()`を維持するdeprecated thin adapter |
| `main.py` | Accept negotiation、transport decode/encode、representation dispatch |

主要APIは次とします。

```python
@dataclass(slots=True)
class CaseSolution:
    case_id: str
    analysis_result: AnalysisResult
    model: FemModel
    compatibility_rate: float

def iter_legacy_beam_case_solutions(
    data: dict[str, Any],
) -> Iterator[CaseSolution]: ...

def solve_analysis_result_set(
    data: dict[str, Any],
) -> AnalysisResultSetV1: ...

def project_frame_case(
    source: dict[str, Any],
    solution: CaseSolution,
) -> FrameCaseResult: ...

def solve_frame_result_set(
    data: dict[str, Any],
) -> FrameResultSetV1: ...

def solve_legacy_frame_map(
    data: dict[str, Any],
) -> dict[str, FrameCaseResult]: ...
```

`iter_legacy_beam_case_solutions()` は一度に一つだけ `CaseSolution` を保持します。Analysis表現ではcanonical resultだけを、Frame表現では投影済みresultだけをaccumulatorへ追加します。

現在の後乗算 `rate` fallbackは [legacy_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/legacy_results.py:88) にあり、v1でもFrame表現だけで維持します。

## 3. Frontend Components and APIs

新規 `result-contracts.ts` に次を置きます。

```typescript
interface FrameCaseResult {
  disg: Record<string, unknown>;
  reac: Record<string, unknown>;
  fsec: Record<string, unknown>;
  shell_fsec: Record<string, unknown>;
  size: number;
}

interface FrameResultSetCaseV1 {
  case_id: string;
  result: FrameCaseResult;
}

interface FrameResultSetV1 {
  kind: "frame_result_set";
  schema_version: "1.0";
  cases: FrameResultSetCaseV1[];
}

interface NormalizedFrameResults {
  caseIds: readonly string[];
  byId: Record<string, FrameCaseResult>;
}
```

境界関数は分離します。

```typescript
normalizeFrameResultSetV1(value, expectedCaseIds): NormalizedFrameResults
normalizeSavedFrameResultMap(value): NormalizedFrameResults
```

- HTTP responseは前者だけで検証する。
- 保存済みraw mapは後者だけで読む。
- `byId`は既存worker用lookup map。
- `caseIds`を表示・worker処理順の正規情報とする。
- `byId`はprototype pollutionを避けるためnull-prototype objectで生成する。
- 保存時は`byId`を保存し、既存ファイル形式を変更しない。

単純にarrayをobjectへ変換するだけでは、整数風case IDが再ソートされます。現在のvalidatorとworkerは `Object.keys()` を使用しているため、[result-data.service.ts](/C:/Users/sasai/Documents/FrameWeb3/FrameWebforJS/src/app/providers/result-data.service.ts:57) と3つのworkerへ `caseIds` を伝播させます。worker lifecycle自体は変更しません。

## 4. Wire Contracts

`AnalysisResultSet v1`:

```json
{
  "kind": "analysis_result_set",
  "schema_version": "1.0",
  "cases": [
    {
      "case_id": "2",
      "result": {
        "analysis_type": "material_nonlinear",
        "step_results": [],
        "convergence_history": []
      }
    }
  ]
}
```

`FrameResultSet v1`:

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

確定事項:

- `schema_version`は文字列 `"1.0"`。
- `case_id`は入力JSON object keyを保持する文字列。
- `cases`は順序付きarray。
- `case_count`は重複情報なので持たない。
- canonical resultへ`rate`を適用しない。
- nonlinear historyは各`AnalysisResult`内に保持する。現在の構造は [solver_results.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/src/fem/solver_results.py:191) と一致します。
- Frame表現はtop-levelの最終受理状態だけを投影する。

legacy aliasだけは従来どおりbare mapです。

```json
{
  "2": {
    "disg": {},
    "reac": {},
    "fsec": {},
    "shell_fsec": {},
    "size": 42
  }
}
```

## 5. Accept Negotiation Rules

| Accept | Result |
|---|---|
| 未指定・空 | default `AnalysisResult` |
| `application/json` | default `AnalysisResult` |
| `application/vnd.frameweb.analysis-result-set-v1+json` | `AnalysisResultSet v1` |
| `application/vnd.frameweb.frame-result-set-v1+json` | `FrameResultSet v1` |
| `application/vnd.frameweb.legacy-cases-v1+json` | deprecated bare map |
| `application/*`、`*/*` | defaultのみ |
| unknown FrameWeb vendor type、`q>0` | 406 |
| 対応候補なし | 406 |

追加規則:

- `q=0`は明示的な除外。
- vendor typeはexact matchのみ。wildcardで高コストなmulti-case処理を開始しない。
- 複数候補は最大`q`を選ぶ。
- defaultとvendorが同じ`q`なら明示vendorを優先。
- 異なるvendor typeが最高`q`で並んだ場合は曖昧として406。
- unknown vendor typeは、default fallbackがあってもpositive `q`なら406。
- `charset`等の非`q`parameterはmatchingでは無視する。
- 不正な`q`は406。

現在の単純なselectorは [main.py](/C:/Users/sasai/Documents/FrameWeb3/FrameWeb/main.py:160) にあるため、これを純粋関数としてテスト可能にします。

## 6. Data Flow

1. body decodeより先にAcceptを確定する。
2. defaultなら現在のsingle-case経路をそのまま実行する。
3. multi-caseならglobal shape、legacy beam制約、1～256件を検証する。
4. 各caseについて`select_case`、fresh `FemModel`、`run()`、JSON-safe変換を実行する。
5. 一時`CaseSolution`を選択表現へ即時変換し、modelを解放する。
6. 全ケース成功後にenvelopeまたはbare mapをJSON化する。
7. AngularはFrame envelopeを検証し、`{caseIds, byId}`へ正規化して既存result servicesへ渡す。

メモリ量は「一つのmodel＋一つの処理中result＋要求された最終response」に限定されます。AnalysisResultSet自体のresult保持はresponse内容なので不可避ですが、全model保持はありません。

## 7. Failure and Compatibility Semantics

- ケース途中で失敗した場合、accumulatorを破棄し、partial responseを返さない。
- error payloadには既存方式で`case_id`を付ける。
- negotiation失敗は解析開始前に406。
- Frame modal投影は`unsupported_analysis`として明示的に拒否する。
- shell/solid/modern-input multi-caseはv1対象外。
- invalid/non-finite `rate`はFrame/legacyのみ`1.0` fallback。
- default single-case response、compression、C# print contractは変更しない。
- legacy aliasはbody、field順、Content-Type、atomicityを維持する。
- 保存済みraw result mapは引き続き読める。
- aliasのobject-key順序制約は従来どおりで、新しい順序保証はarray envelopeだけが提供する。

## 8. Test Strategy

Backend:

- default `application/json`の既存schema完全不変。
- 2つの新media typeとContent-Type。
- `"2"`, `"1"`, `"01"`, `"a"`の順序保持。
- unknown version、wildcard、parameters、`q=0`、競合vendor。
- 256件境界をmodel生成前に検証。
- fresh model/caseと入力非変更。
- 後半case失敗時のatomicity。
- canonical未scale、Frame/aliasのexactly-once rate。
- 非有限rateのv1 fallback。
- nonlinear caseごとのhistory保持とFrame final-state-only。
- modal Frame投影拒否。
- legacy aliasと旧fixtureのJSON同値。

Frontend:

- exact kind/version/case ID/orderの検証。
- duplicate、missing、extra caseの拒否。
- 5フィールド、positive integer `size`、有限数値leafの検証。
- malformed/empty responseでworkerを開始しない。
- integer-like IDでも`caseIds`順をworkerへ渡す。
- 保存済みraw mapの読込回帰。
- HTTPだけ新media typeへ移行すること。
- Ctの先頭・末尾caseを使ったbrowser確認。

## 9. File-Level Change Map

Backend:

- `FrameWeb/main.py`
- 新規 `FrameWeb/src/fem/result_contracts.py`
- 新規 `FrameWeb/src/fem/result_sets.py`
- 新規 `FrameWeb/src/fem/frame_results.py`
- `FrameWeb/src/fem/legacy_results.py`
- `FrameWeb/tests/io/test_legacy_cases_api.py`
- 新規 `FrameWeb/tests/io/test_result_set_api.py`
- nonlinear/model contract tests
- endpoint/result documentation

Frontend:

- `FrameWebforJS/src/app/app.component.ts`
- 新規 `providers/result-contracts.ts`
- `providers/result-data.service.ts/.spec.ts`
- displacement/reaction/section-force serviceとworkerの`caseIds`伝播箇所
- HTTP contract tests

Design:

- `.agents/docs/DESIGN.md`の「`FrameResultSet`は`AnalysisResultSet`の直接projection」という記述を、内部`CaseSolution`からの兄弟representationへ修正する必要があります。

## 10. Open Decisions

v1実装を妨げる未決事項はありません。

別機能として延期するもの:

- legacy aliasのSunset日と削除条件
- `rate`をsolver load factorとして扱うv2
- modern input、shell、solidのmulti-case対応
- worker completion lifecycle全体の再設計
- 保存ファイル自体のenvelope化

## 11. Verdict

**APPROVED**

この設計で実装へ進めます。既存briefの「10～14ファイル」という見積りは、整数風case IDの順序をworkerまで保証する変更を含めるとやや少なく、実際はテスト・文書込みで15～20ファイル程度を見込むのが安全です。
