# FrameWeb monorepo

## Visual Studio から起動する

必要なもの（Windows）:

- Visual Studio 2022 17.11 以降、または Visual Studio 2026。「ASP.NET と Web 開発」ワークロードと .NET 8 の開発ツールをインストールしてください。
- `uv`（Python 環境の準備用）
- Node.js / npm（初回のローカルツール導入用。システムの Node のバージョンは変更しません）
- 初回セットアップ時のインターネット接続

1. リポジトリ直下の **`FrameWeb.sln`** を Visual Studio で開きます。
2. 起動対象 **`FrameWeb.Startup`** または共有プロファイル **`FrameWeb - all services`** を選び、**F5** を押します。
3. ブラウザに起動状況が表示されます。初回は Python / npm の依存関係を導入し、Angular をビルドするため数分かかります。
4. 準備ができると `http://127.0.0.1:4200/` に移動します。

| サービス | URL | 実行内容 |
|---|---|---|
| 解析 | `http://127.0.0.1:8080/` | `FrameWeb/main.py` の Flask アプリ |
| 印刷 | `http://127.0.0.1:7071/api/Function1` | `FramePrintPDF/FramePrintAzure/Function1.cs` の既存処理 |
| フロントエンド | `http://127.0.0.1:4200/` | `FrameWebforJS` の Angular 開発サーバー |
| 起動状況 | `http://127.0.0.1:7071/` | 準備・起動エラーの表示 |

**Shift+F5 で停止すると、解析・印刷・フロントエンドも終了します。** 起動プロジェクトが強制終了された場合も、Windows Job Object によりその子プロセスを終了します。使用中のポートを持つ他のプロセスは終了しません。

共有プロファイルが表示されない Visual Studio では `FrameWeb.Startup` を右クリックして「スタートアップ プロジェクトに設定」を選んでください。複数のスタートアッププロジェクトを手作業で登録する必要はありません。[Visual Studio の共有起動プロファイルについて](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects)。

## フロントエンドのソースを編集する

`FrameWeb.sln` のソリューション エクスプローラーで **`FrameWebforJS`** プロジェクトを展開すると、`src/app` 以下の TypeScript・HTML・SCSS、`src/assets`、`src/environments`、各種設定ファイルを編集できます。新しく追加したソースファイルも自動で表示されます。`node_modules`、`.angular`、`dist` などの依存関係・生成物は表示対象から除外しています。

`FrameWebforJS` が「非互換」と表示される場合は、まず Visual Studio Installer で **「ASP.NET と Web 開発」** と **JavaScript / TypeScript のプロジェクト対応**が導入済みか確認してください。[Microsoft の Angular プロジェクトの前提条件](https://learn.microsoft.com/en-us/visualstudio/javascript/tutorial-asp-net-core-with-angular#prerequisites)も参照してください。

開発ツールが導入済みでも、ソリューションに以前の読み込み失敗の状態が残ることがあります。`FrameWebforJS` を右クリックして「プロジェクトの再読み込み」を試してください。改善しない場合は Visual Studio を閉じ、`.vs/FrameWeb/v18/.suo`（Visual Studio 2022 では `v17`）を別名に退避してから `FrameWeb.sln` を開き直します。`.suo` には起動対象やブレークポイントなどのユーザー設定も含まれるため、バックアップを残してください。再生成後は起動対象の **`FrameWeb.Startup`** または **`FrameWeb - all services`** を選び直します。

起動対象は **`FrameWeb.Startup`** または **`FrameWeb - all services`** を選んでください。依存関係の導入と Angular 開発サーバーの起動は従来どおり起動プロジェクトが担当するため、`FrameWebforJS.esproj` のビルド時には `npm install` や本番用の `npm run build` を実行しません。

## ローカル設定とログ

初回起動で `scripts/setup-local.ps1` が次を準備します。

- `tools/local-tools/node_modules` に Node **18.20.8** / npm **9.9.4** を導入（Angular の `engines` に合わせた専用環境）。
- `FrameWeb/.venv` に Python **3.12** と `uv.lock` の依存関係を導入。
- `FrameWebforJS/node_modules` にフロントの依存関係を導入。
- `environment.local.example.ts` から未作成の `environment.ts` / `environment.local.ts` を作成。既存ファイルは上書きしません。

Visual Studio から起動する Angular は `local` 構成を使い、**`FrameWebforJS/src/environments/environment.local.ts`** を読み込みます。接続先や認証設定を変更する場合はこのファイルを編集してください。このファイルは Git 管理対象外です。テンプレート内の認証値は起動用の仮設定であり、ログイン機能を提供するものではありません。

F5 または `npm run start:local` で使う `local` 構成では、`environment.visualstudio.ts` が上記設定を引き継ぎ、`allowAnonymousCalculation: true` によりログインなしで計算を開始できます。未ログイン時のユーザー ID は空文字列で送信し、ログイン済みの場合は従来のユーザー ID を使います。通常構成と本番構成ではログインが必要です。ログイン機能自体は従来どおり利用できます。

ログは Visual Studio の出力／コンソールと `.local/logs/{setup,engine,frontend}.log` に出ます。ブラウザの起動状況ページにも準備完了や失敗理由が表示されます。Python の起動には仮想環境の activate は不要です。

CLI でも同じ構成を起動できます。

```powershell
dotnet run --project tools/FrameWeb.Startup
# ブラウザで http://127.0.0.1:7071/ を開く
```

セットアップのみ、または依存関係を入れ直す場合:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-local.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-local.ps1 -ForceDependencies
```

起動・解析 API・PDF・停止後のプロセス終了・ポート競合をまとめて確認するには、開発サーバーを停止した状態で次を実行します。

```powershell
dotnet build FrameWeb.sln
FrameWeb\.venv\Scripts\python.exe scripts/smoke-local.py
```

初回起動が失敗した場合は `setup.log` のエラーを確認してください。Node.js / uv をインストールした後は、PATH を再読込するため Visual Studio を起動し直してください。ポート 4200 / 7071 / 8080 を使用する以前の開発サーバーがある場合は、先に停止してください。

## 起動構成の範囲

`FrameWeb.Startup` が印刷 HTTP ホストを兼ね、解析・Angular を子プロセスとして起動します。印刷は既存の Azure Functions ハンドラーをそのまま呼ぶため、ローカル実行に Azure Functions Core Tools やストレージ接続情報は不要です。Azure へのデプロイには従来の `FramePrintAzure` プロジェクトを使います。C# の印刷処理は F5 のデバッガー対象です。Python / TypeScript のブレークポイントにはそれぞれのデバッガーの接続が必要です。

この構成はサーバーの起動とローカル接続設定を用意します。解析の圧縮・結果形式の互換性（[#2](https://github.com/sasaco/fempy/issues/2)、[#3](https://github.com/sasaco/fempy/issues/3)）、入力表の問題（[#10](https://github.com/sasaco/fempy/issues/10)）、実サービスの認証設定は別途対応が必要です。`FrameGConverter` は今回の起動対象には含めていません。
