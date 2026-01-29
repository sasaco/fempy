# エラーハンドリング

## 概要
FrameWeb3は、開発者が構造解析リクエストの問題を特定し解決するのに役立つ包括的なエラーハンドリングを提供します。APIは標準的なHTTPステータスコードを使用し、JSON形式で詳細なエラー情報を返します。

## HTTPステータスコード

### 200 OK
解析が正常に完了しました。レスポンスにはすべての荷重ケースの計算結果が含まれます。

### 400 Bad Request
無効な入力データ。以下の場合に発生します：
- 必須フィールドが欠如している
- データ形式が正しくない
- 参照されるエンティティが存在しない（例：存在しない節点を参照）
- 無効な数値

### 500 Internal Server Error
解析計算エラー。以下の場合に発生します：
- 構造解析が収束しない
- 特異剛性行列（不安定構造）
- 数値計算エラー
- システムレベルエラー

## エラーレスポンス形式
すべてのエラーレスポンスは一貫したJSON構造に従います：

```json
{
  "error": "エラーカテゴリ",
  "message": "詳細なエラーメッセージ",
  "details": {
    "node": 5,
    "member": 3,
    "loadCase": "DL",
    "caseComb": {
      "nMaterialCase": 1,
      "nSupportCase": 1,
      "nSpringCase": 1,
      "nJointCase": 1
    }
  }
}
```

## 一般的なエラータイプ

### 入力データエラー (400)

#### 必須データの欠如
```json
{
  "error": "入力データエラー",
  "message": "節点データがありません",
  "details": {}
}
```

#### 無効な節点参照
```json
{
  "error": "入力データエラー",
  "message": "存在しない節点が参照されています: 節点番号(5)",
  "details": {
    "node": 5,
    "member": 3
  }
}
```

#### 材料特性の欠如
```json
{
  "error": "入力データエラー",
  "message": "材料特性データが不足しています: 材料番号(2)",
  "details": {
    "material": 2,
    "member": 1
  }
}
```

#### 無効な荷重データ
```json
{
  "error": "入力データエラー",
  "message": "荷重データが不正です: 荷重ケース(LL)",
  "details": {
    "loadCase": "LL",
    "node": 3
  }
}
```

### 解析計算エラー (500)

#### 特異剛性行列
```json
{
  "error": "計算エラー",
  "message": "剛性行列の特異性により解析が収束しませんでした",
  "details": {
    "loadCase": "DL",
    "caseComb": {
      "nMaterialCase": 1,
      "nSupportCase": 1,
      "nSpringCase": 1,
      "nJointCase": 1
    }
  }
}
```

#### 数値不安定性
```json
{
  "error": "計算エラー",
  "message": "数値計算が不安定になりました",
  "details": {
    "loadCase": "WL",
    "member": 5
  }
}
```

#### メモリ割り当てエラー
```json
{
  "error": "システムエラー",
  "message": "メモリ不足により計算を継続できません",
  "details": {
    "nodeCount": 10000,
    "elementCount": 25000
  }
}
```

## エラーハンドリングのベストプラクティス

### 1. 入力データの検証
リクエストを送信する前に、常に入力データを検証してください：

```python
def validate_model_data(model_data):
    """解析前に構造モデルデータを検証"""
    errors = []
    
    # 必須セクションの確認
    required_sections = ['node', 'element', 'load']
    for section in required_sections:
        if section not in model_data:
            errors.append(f"必須セクションが欠如しています: {section}")
    
    # 部材の節点参照を検証
    if 'member' in model_data and 'node' in model_data:
        node_ids = set(model_data['node'].keys())
        for member_id, member in model_data['member'].items():
            if str(member['ni']) not in node_ids:
                errors.append(f"部材 {member_id} が存在しない節点 {member['ni']} を参照しています")
            if str(member['nj']) not in node_ids:
                errors.append(f"部材 {member_id} が存在しない節点 {member['nj']} を参照しています")
    
    # 要素特性参照を検証
    if 'member' in model_data and 'element' in model_data:
        element_ids = set(model_data['element'].keys())
        for member_id, member in model_data['member'].items():
            if str(member['e']) not in element_ids:
                errors.append(f"部材 {member_id} が存在しない要素 {member['e']} を参照しています")
    
    return errors

# 使用例
errors = validate_model_data(model_data)
if errors:
    print("検証エラー:")
    for error in errors:
        print(f" - {error}")
    return
```

### 2. APIエラーの適切な処理
```python
import requests
import json

def analyze_structure_with_error_handling(model_data):
    """包括的なエラーハンドリングを含む構造解析の実行"""
    try:
        response = requests.post(
            'http://localhost:5000/',
            json=model_data,
            headers={'Content-Type': 'application/json'},
            timeout=60
        )
        
        if response.status_code == 200:
            return response.json()
        elif response.status_code == 400:
            error_data = response.json()
            print(f"入力エラー: {error_data['message']}")
            
            # 特定のエラータイプの処理
            if 'details' in error_data:
                details = error_data['details']
                if 'node' in details:
                    print(f"節点の問題: {details['node']}")
                if 'member' in details:
                    print(f"部材の問題: {details['member']}")
                if 'loadCase' in details:
                    print(f"荷重ケースの問題: {details['loadCase']}")
            return None
        elif response.status_code == 500:
            error_data = response.json()
            print(f"解析エラー: {error_data['message']}")
            
            # 構造不安定性の確認
            if "特異性" in error_data['message']:
                print("構造が不安定な可能性があります。支点条件を確認してください。")
            return None
        else:
            print(f"予期しないエラー: HTTP {response.status_code}")
            print(response.text)
            return None
            
    except requests.exceptions.Timeout:
        print("リクエストがタイムアウトしました。解析に時間がかかりすぎている可能性があります。")
        return None
    except requests.exceptions.ConnectionError:
        print("FrameWeb3 APIに接続できませんでした。サービスが実行されているか確認してください。")
        return None
    except json.JSONDecodeError:
        print("サーバーから無効なJSONレスポンスを受信しました")
        return None
    except Exception as e:
        print(f"予期しないエラー: {e}")
        return None
```

### 3. 一時的エラーのリトライロジック
```python
import time
from typing import Optional

def analyze_with_retry(model_data, max_retries=3, retry_delay=1.0):
    """一時的エラーに対するリトライロジックを含む構造解析"""
    for attempt in range(max_retries):
        try:
            response = requests.post(
                'http://localhost:5000/',
                json=model_data,
                headers={'Content-Type': 'application/json'},
                timeout=60
            )
            
            if response.status_code == 200:
                return response.json()
            elif response.status_code == 400:
                # 入力エラーはリトライしない
                error_data = response.json()
                print(f"入力エラー（リトライなし）: {error_data['message']}")
                return None
            elif response.status_code == 500:
                error_data = response.json()
                if attempt < max_retries - 1:
                    print(f"サーバーエラー（試行 {attempt + 1}/{max_retries}）: {error_data['message']}")
                    print(f"{retry_delay}秒後にリトライします...")
                    time.sleep(retry_delay)
                    retry_delay *= 2  # 指数バックオフ
                    continue
                else:
                    print(f"サーバーエラー（最終試行）: {error_data['message']}")
                    return None
                    
        except requests.exceptions.Timeout:
            if attempt < max_retries - 1:
                print(f"タイムアウト（試行 {attempt + 1}/{max_retries}）。リトライします...")
                time.sleep(retry_delay)
                retry_delay *= 2
                continue
            else:
                print("最終タイムアウト。解析に失敗しました。")
                return None
                
        except requests.exceptions.ConnectionError:
            if attempt < max_retries - 1:
                print(f"接続エラー（試行 {attempt + 1}/{max_retries}）。リトライします...")
                time.sleep(retry_delay)
                retry_delay *= 2
                continue
            else:
                print("最終接続エラー。サービスがダウンしている可能性があります。")
                return None
    
    return None
```

## 一般的な問題のトラブルシューティング

### 構造不安定性
**問題**: "剛性行列の特異性により解析が収束しませんでした"

**解決策**:
1. 支点条件を確認
   - 構造が適切に拘束されていることを確認
2. すべての部材が正しく接続されていることを確認
3. ゼロまたは非常に小さい断面特性がないか確認
4. 材料特性が現実的であることを確認

```python
def check_structural_stability(model_data):
    """構造安定性の基本チェック"""
    issues = []
    
    # 支点があるかチェック
    if 'fix_node' not in model_data or not model_data['fix_node']:
        issues.append("支点条件が定義されていません - 構造が不安定な可能性があります")
    
    # 非常に小さい断面特性をチェック
    if 'element' in model_data:
        for elem_id, elem in model_data['element'].items():
            if 'A' in elem and elem['A'] < 1e-6:
                issues.append(f"要素 {elem_id} の断面積が非常に小さいです: {elem['A']}")
            if 'E' in elem and elem['E'] < 1000:
                issues.append(f"要素 {elem_id} のヤング係数が非常に小さいです: {elem['E']}")
    
    return issues
```

### 大規模モデルの性能
**問題**: 解析に時間がかかりすぎる、またはメモリ不足

**解決策**:
1. 大きなリクエストにはモデル圧縮を使用
2. 可能な場合はモデルを簡略化
3. 不要な要素分割がないか確認
4. プレートには多数の梁要素の代わりにシェル要素の使用を検討

### 無効な参照
**問題**: 存在しない節点、材料などへの参照

**解決策**:
1. リクエスト送信前にすべての参照を検証
2. 一貫した番号付けスキームを使用
3. 節点・要素番号のタイプミスをチェック

## エラーコードリファレンス

| エラータイプ | HTTPコード | カテゴリ | 説明 |
|------------|-----------|----------|-------------|
| 必須データの欠如 | 400 | 入力データエラー | 入力から必須セクションが欠如 |
| 無効な節点参照 | 400 | 入力データエラー | 部材が存在しない節点を参照 |
| 無効な材料参照 | 400 | 入力データエラー | 要素が存在しない材料を参照 |
| 無効な荷重データ | 400 | 入力データエラー | 荷重ケースに無効なデータが含まれる |
| 特異剛性行列 | 500 | 計算エラー | 構造が不安定または不適切に拘束 |
| 数値不安定性 | 500 | 計算エラー | 数値問題により計算が失敗 |
| メモリ割り当てエラー | 500 | システムエラー | 解析に必要なメモリが不足 |
| タイムアウトエラー | 500 | システムエラー | 解析が制限時間を超過 |

追加サポートについては、動作するコードサンプルの[使用例](examples.md)ドキュメントと、詳細な入力形式仕様の[データ構造](data-structures.md)リファレンスを参照してください。
