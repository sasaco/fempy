namespace FrameWeb.Startup;

internal static class StartupPage
{
    public const string Html = """
        <!doctype html>
        <html lang="ja"><meta charset="utf-8"><title>FrameWeb 起動</title>
        <style>body{font:16px system-ui;max-width:720px;margin:64px auto;padding:24px;color:#243444}li{margin:12px 0}#message{white-space:pre-wrap}</style>
        <h1>FrameWeb を起動しています</h1>
        <p id="message">ローカル環境を準備しています。初回は依存関係の導入とビルドに数分かかります。</p>
        <ul>
          <li>解析サーバー：<a href="http://127.0.0.1:8080/">http://127.0.0.1:8080/</a></li>
          <li>印刷サーバー：<a href="/api/Function1?test=FrameWeb">http://127.0.0.1:7071/api/Function1</a></li>
          <li>フロントエンド：<a href="http://127.0.0.1:4200/">http://127.0.0.1:4200/</a></li>
        </ul>
        <p>起動が完了するとフロントエンドに移動します。詳細は Visual Studio の出力、または .local/logs を確認してください。</p>
        <script>
        async function check() {
          try {
            const response = await fetch('/health/ready', {cache:'no-store'});
            const state = await response.json();
            document.getElementById('message').textContent = state.message;
            if (state.status === 'ready') { location.replace('http://127.0.0.1:4200/'); return; }
            if (state.status === 'failed') { document.querySelector('h1').textContent = '起動できませんでした'; return; }
          } catch (_) { document.getElementById('message').textContent = '起動プロジェクトとの接続を確認しています。'; }
          setTimeout(check, 1000);
        }
        check();
        </script></html>
        """;
}
