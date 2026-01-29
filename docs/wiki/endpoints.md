# APIエンドポイント

## ベースエンドポイント

**URL**: `/`
**メソッド**: `GET`, `POST`
**Content-Type**: `application/json`

## POST / - 構造解析

提供されたモデルデータに対して構造解析を実行します。

### リクエスト

#### ヘッダー

- `Content-Type: application/json`
- `Content-Encoding: gzip` (オプション、圧縮リクエスト用)

#### リクエストボディ

リクエストボディには構造モデルを表すJSONオブジェクトを含める必要があります。データはgzipエンコーディングを使用してオプションで圧縮できます。

**基本構造:**
```json
{
    "node": { ... },
    "member": { ... },
    "element": { ... },
    "fix_node": { ... },
    "load": { ... }
}
```

**圧縮サポート:**
APIは大きなモデルに対してgzip圧縮をサポートします。圧縮データを送信する場合：

1. JSON文字列をgzipで圧縮
2. 圧縮データをbase64でエンコード
3. `Content-Encoding: gzip` ヘッダーを設定

### レスポンス

#### 成功レスポンス (200 OK)

すべての荷重ケースの解析結果を返します。

```json
{
    "loadCase1": {
        "disg": { ... },
        "reac": { ... },
        "fsec": { ... },
        "shell_fsec": { ... },
        "shell_results": { ... },
        "size": 100
    },
    "loadCase2": { ... }
}
```

#### レスポンス圧縮

大きなレスポンスは以下の場合に自動的にgzipで圧縮され、base64エンコードされます：

- レスポンスサイズが内部閾値を超える場合
- クライアントが圧縮をサポートする場合

圧縮レスポンスには以下が含まれます：

- `Content-Encoding: gzip` ヘッダー
- Base64エンコードされたgzip圧縮JSONデータ

### エラーレスポンス

#### 400 Bad Request

無効な入力データ形式または必須フィールドの欠如。

```json
{
    "error": "入力データエラー",
    "message": "節点データが不足しています",
    "details": {
        "node": 5,
        "member": 3
    }
}
```

#### 500 Internal Server Error

解析計算エラーまたはシステム障害。

```json
{
    "error": "計算エラー",
    "message": "剛性行列の特異性により解析が収束しませんでした",
    "details": {
        "loadCase": "case1",
        "caseComb": {
            "nMaterialCase": 1,
            "nSupportCase": 1,
            "nSpringCase": 1,
            "nJointCase": 1
        }
    }
}
```

## GET / - ヘルスチェック

基本的なサービス情報とヘルス状態を返します。

### リクエスト

リクエストボディは不要です。

### レスポンス

```json
{
    "service": "FrameWeb3",
    "status": "running",
    "version": "3.0",
    "timestamp": "2025-05-31T12:35:04Z"
}
```

## リクエスト/レスポンス例

### シンプルな2Dフレーム解析

**リクエスト:**
```bash
curl -X POST http://localhost:5000/ \
-H "Content-Type: application/json" \
-d '{
    "node": {
        "1": {"x": 0, "y": 0},
        "2": {"x": 5, "y": 0},
        "3": {"x": 5, "y": 3}
    },
    "member": {
        "1": {"ni": 1, "nj": 2, "e": 1},
        "2": {"ni": 2, "nj": 3, "e": 1}
    },
    "element": {
        "1": {
            "E": 205000000,
            "G": 79000000,
            "Iy": 0.0001,
            "Iz": 0.0001,
            "J": 0.0001,
            "A": 0.01
        }
    },
    "fix_node": {
        "1": {"1": {"x": 1, "y": 1, "rx": 1, "ry": 1, "rz": 1}}
    },
    "load": {
        "DL": {
            "load_node": [{"n": 3, "ty": -50}]
        }
    }
}'
```

**レスポンス:**
```json
{
    "DL": {
        "disg": {
            "1": {"dx": 0, "dy": 0, "dz": 0, "rx": 0, "ry": 0, "rz": 0},
            "2": {"dx": 0.0012, "dy": -0.0008, "dz": 0, "rx": 0, "ry": 0, "rz": -0.0003},
            "3": {"dx": 0.0024, "dy": -0.0015, "dz": 0, "rx": 0, "ry": 0, "rz": -0.0005}
        },
        "reac": {
            "1": {"tx": -25, "ty": 50, "tz": 0, "mx": 0, "my": 0, "mz": 75}
        },
        "fsec": {
            "1": {
                "1": {
                    "fxi": 25,
                    "fyi": 0,
                    "fzi": 0,
                    "mxi": 0,
                    "myi": 0,
                    "mzi": 0,
                    "fxj": -25,
                    "fyj": 0,
                    "fzj": 0,
                    "mxj": 0,
                    "myj": 0,
                    "mzj": 0,
                    "L": 5
                }
            },
            "2": {
                "1": {
                    "fxi": 0,
                    "fyi": 50,
                    "fzi": 0,
                    "mxi": 0,
                    "myi": 0,
                    "mzi": 0,
                    "fxj": 0,
                    "fyj": -50,
                    "fzj": 0,
                    "mxj": 0,
                    "myj": 0,
                    "mzj": 150,
                    "L": 3
                }
            }
        },
        "shell_fsec": {},
        "shell_results": {},
        "size": 3
    }
}
```

### シェル要素を含む3Dフレーム

**リクエスト:**
```bash
curl -X POST http://localhost:5000/ \
-H "Content-Type: application/json" \
-d '{
    "node": {
        "1": {"x": 0, "y": 0, "z": 0},
        "2": {"x": 4, "y": 0, "z": 0},
        "3": {"x": 4, "y": 4, "z": 0},
        "4": {"x": 0, "y": 4, "z": 0}
    },
    "shell": {
        "1": {"ni": 1, "nj": 2, "nk": 3, "nl": 4, "e": 1}
    },
    "element": {
        "1": {"E": 30000000, "G": 12000000, "poi": 0.2}
    },
    "thickness": {
        "1": {"t": 0.2}
    },
    "fix_node": {
        "1": {"1": {"x": 1, "y": 1, "z": 1, "rx": 1, "ry": 1, "rz": 1}}
    },
    "load": {
        "LL": {
            "load_node": [{"n": 3, "tz": -100}]
        }
    }
}'
```

## レート制限

現在、レート制限は実装されていません。ただし、本番環境での展開では、インフラストラクチャ要件に基づいて適切なレート制限の実装を検討してください。

## 認証

現在のAPIは認証を必要としません。本番環境での展開では、APIキーやOAuthトークンなどの適切な認証メカニズムを実装してください。
