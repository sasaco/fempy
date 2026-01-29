# FrameWeb3 をuv環境に変換する計画

## 概要
現在の `requirements.txt` ベースのプロジェクトを `uv` パッケージマネージャー環境に移行します。

## 変更内容

### 1. `pyproject.toml` を新規作成
ファイル: [pyproject.toml](pyproject.toml)

```toml
[project]
name = "frameweb3"
version = "1.0.0"
description = "Python FEM解析モジュール"
requires-python = ">=3.11"
dependencies = [
    "flask>=3.0.3",
    "functions-framework>=3.8.1",
    "numpy>=2.1.0",
    "scipy>=1.14.1",
    "js2py",
]

[project.optional-dependencies]
dev = [
    "pytest>=8.3.2",
    "flet>=0.23.2",
]

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"
```

### 2. `.gitignore` を更新
ファイル: [.gitignore](.gitignore)

追加項目:
```
.venv/
uv.lock
```

### 3. `requirements.txt` は維持
互換性のためそのまま残します。

### 4. Dockerfile は変更なし
pipを使用する現行の構成を維持します。

## 変更対象ファイル
| ファイル | 操作 |
|---------|------|
| `pyproject.toml` | 新規作成 |
| `.gitignore` | 更新（2行追加） |

## 検証手順
```bash
# 1. uv で仮想環境を作成し依存関係をインストール
uv sync

# 2. 開発用依存関係もインストール
uv sync --extra dev

# 3. テスト実行
uv run pytest tests/

# 4. Flaskサーバー起動確認
uv run flask run
```
