実装計画案は独立レビューで **PASS** です。ただし現在のセッションは読み取り専用のため、保存先 `.agents/docs/plans/result-contract-refactor.md` の作成と機械検証は実行できていません。

## Implementation Plan: Result Contract Refactor

### Purpose

単一解析・複数ケース解析・FrameWebforJS表示結果を、それぞれ `AnalysisResult`、`AnalysisResultSet`、`FrameResultSet` として明確に分離します。既定APIと既存保存ファイルを維持しながら、順序・rate・非線形履歴・互換表現の責務を固定します。

### Scope

新規ファイル:

- `FrameWeb/src/fem/result_contracts.py`
- `FrameWeb/src/fem/result_sets.py`
- `FrameWeb/src/fem/frame_results.py`
- `FrameWeb/tests/io/test_result_set_api.py`
- `FrameWebforJS/src/app/providers/result-contracts.ts`
- `FrameWebforJS/src/app/providers/result-contracts.spec.ts`

主な変更ファイル:

- `FrameWeb/main.py`
- `FrameWeb/src/fem/legacy_results.py`
- `FrameWeb/tests/io/test_legacy_cases_api.py`
- `FrameWeb/tests/io/test_http.py`
- `FrameWeb/tests/solvers/test_nonlinear.py`
- `FrameWeb/tests/integration/test_model_contracts.py`
- `FrameWeb/docs/wiki/endpoints.md`
- `FrameWeb/docs/wiki/results.md`
- `FrameWebforJS/src/app/app.component.ts`
- `FrameWebforJS/src/app/providers/result-data.service.ts`
- `FrameWebforJS/src/app/providers/result-data.service.spec.ts`
- displacement/reaction/section-force の3サービスと3つの `*1.worker.ts`
- 必要最小限の `FrameWebforJS/angular.json`
- `.agents/docs/DESIGN.md`

依存パッケージの追加はありません。

### Implementation Steps

#### Step 1: 既存backend契約のcharacterization

- [ ] 既定 `AnalysisResult` のschemaを固定する。
- [ ] `legacy-cases-v1` のbare map、field順、Content-Type、rate、原子性を固定する。
- [ ] 0/256/257ケース境界、fresh model、入力非変更、modal時の現行エラーを固定する。

**Verification**: 既存legacy/APIテストが変更前後で同一結果になること。

#### Step 2: Angularテスト基盤のbaseline確立

- [ ] `src/test.ts`、`src/polyfills.ts`の存在を確認する。
- [ ] 現在の存在しない `@fortawesome/some-free/...` script参照だけを最小修正する。
- [ ] Karmaがspecを0件ではなく実行することを完了条件とする。

**Verification**: ChromeHeadlessで既存specが1件以上実行され、成功すること。

#### Step 3: 契約freezeとDESIGN整合

- [ ] `FrameResultSet`を、wire `AnalysisResultSet`の直接変換ではなく、ephemeral `CaseSolution`から生成する兄弟representationとして記録する。
- [ ] media type、envelope、Accept競合規則、rate、modal、保存raw mapの許容範囲を固定する。
- [ ] `{caseIds, byId}` をfrontend内の正式な順序・lookup契約とする。

**Verification**: `.agents/check.ps1` と設計差分レビュー。ここを通過するまで並列実装を開始しない。

#### Step 4: Backend契約境界

- [ ] `ResultRepresentation`、TypedDict、`analysis_type`別validatorを追加する。
- [ ] Accept negotiationを純粋関数化する。
- [ ] malformed `q`、`q=0`、wildcard、parameter、unknown/tieをテストする。
- [ ] 406時はdecode・model生成を開始しない。

**Verification**: negotiationとvariant validationのfocused tests。

#### Step 5: Case実行とFrame投影の分離

- [ ] 1～256件を事前検証し、caseごとにfresh `FemModel`を生成する。
- [ ] 一時的な `CaseSolution`だけをprojectorへ渡す。
- [ ] accumulatorにはwire resultだけを保持し、model参照を残さない。
- [ ] canonical結果はunscaled、Frame/aliasだけrateをexactly once適用する。
- [ ] `legacy_results.py`はthin deprecated adapterとして残す。

**Verification**: `"2","1","01","a"`の順序、input immutability、model解放、後半case失敗時のpartial responseなし、旧projectionとの同値性。

#### Step 6: HTTP representation統合

- [ ] `POST /`でdefault、AnalysisResultSet、FrameResultSet、legacy aliasをdispatchする。
- [ ] 新契約はordered `cases` arrayと正確なContent-Typeを返す。
- [ ] nonlinear履歴を各case内に保持する。
- [ ] modalはAnalysisResultSetでは許可し、Frame projectionでは明示拒否する。
- [ ] 圧縮transportとresponse representationを独立させる。

**Verification**: `test_result_set_api.py`、`test_http.py`、圧縮・非圧縮contract tests。

#### Step 7: Frontend normalizer

- [ ] strictなHTTP envelope normalizerと、保存raw map用normalizerを分離する。
- [ ] `kind`、`schema_version`、5 fields、case ID重複、finite値、正整数sizeを検証する。
- [ ] 空のcasesと全結果mapが空のcaseを拒否する。
- [ ] 保存用`byId`とreaction service向けmutable copyを分離する。
- [ ] validation失敗時は3サービス／workerを開始しない。

**Verification**: 新規normalizer specと`result-data.service.spec.ts`。

#### Step 8: Case順序の全経路伝播

- [ ] `caseIds`をResultDataService、3サービス、3workerへ渡す。
- [ ] worker loop、callback後処理、LL列、combine/pickup、UI case listで同じ順序を使う。
- [ ] `Object.keys()`はnode/member/pointなどcase以外のmapに限定する。

**Verification**: integer-like case IDがworker出力・UIリストまで順序を維持すること。

#### Step 9: Angular HTTP client移行

- [ ] `frame-result-set-v1`を要求する。
- [ ] strict normalization成功後だけworkerへdispatchする。
- [ ] 保存対象にはenvelopeではなく従来互換のraw `byId`を渡す。
- [ ] menu/presetの既存raw-map読込を維持する。
- [ ] schema不正時は`isCalculated=false`を維持する。

**Verification**: Angular tests、production build、malformed response回帰。

#### Step 10: 文書・統合・E2E

- [ ] endpoint/result文書へmedia type、case/step、rate、beam-only制約を記載する。
- [ ] backend、Angular、.NETの関連gateを実行する。
- [ ] Ctプリセットのcase 1と11で変位・反力・断面力を確認する。
- [ ] DEFINE/COMBINE/PICKUPとconsole/worker errorなしを確認する。

**Verification**:

```powershell
uv run --project FrameWeb --locked --extra dev python -m pytest FrameWeb/tests -q
npm --prefix FrameWebforJS run test -- --watch=false --browsers=ChromeHeadless
npm --prefix FrameWebforJS run build
dotnet build FrameWeb.sln
dotnet run --project tools/FrameWeb.Startup
```

#### Parallel Work Packages

契約freeze後に実行します。

- Backend担当: `FrameWeb/`の製品コード、テスト、backend文書
- Frontend担当: `FrameWebforJS/`の製品コード、テスト、テスト設定
- Lead担当: `DESIGN.md`、統合、E2E、最終レビュー

複雑度は **COMPLEX**、実装ルートは `team-execute` です。

### Risks & Considerations

- 最大256個の`FemModel`を保持しない。
- case順序をobject key列挙へ戻さない。
- reaction serviceのin-place変更を保存結果へ逆流させない。
- static/nonlinear/modalを単一必須schemaで検証しない。
- canonical結果へ互換rateを混入させない。
- default API、legacy alias、圧縮、C#印刷契約を変更しない。
- 変更済みの`DESIGN.md`はlead専有とし、既存差分を上書きしない。

### Open Questions

実装を妨げる未決事項はありません。次は別機能として延期します。

- legacy aliasのSunset日
- rateをsolver load factor化するv2
- modern input、shell、solidのmulti-case対応
- worker lifecycle全体の再設計
- 保存ファイル自体のenvelope化

保存予定は `.agents/docs/plans/result-contract-refactor.md`、slugは `result-contract-refactor` です。書き込み可能なセッションで、文書作成・shape/artifact検証・最終Codex gateを通してから実装承認へ進めます。
