using System.Net;
using System.Net.Sockets;

namespace FrameWeb.Startup;

internal sealed class LocalServices(StartupState state, IHostApplicationLifetime lifetime,
    ILogger<LocalServices> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start the status/print HTTP host first so F5 can show startup progress.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        try
        {
            await started.Task.WaitAsync(stoppingToken);
            var root = FindRepositoryRoot();
            var logs = Path.Combine(root, ".local", "logs");
            using var job = new WindowsProcessJob();
            EnsurePortAvailable(8080);
            EnsurePortAvailable(4200);

            state.Set("starting", "依存関係とローカル設定を準備しています。初回は数分かかります。");
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            using (var setup = new ManagedProcess("setup", powershell, root,
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(root, "scripts", "setup-local.ps1")], logs, job))
            {
                await setup.WaitForExitAsync(stoppingToken);
                if (setup.ExitCode != 0)
                    throw new InvalidOperationException("環境の準備に失敗しました。.local/logs/setup.log を確認してください。");
            }

            // Recheck after installation: never adopt/stop somebody else's server.
            EnsurePortAvailable(8080);
            EnsurePortAvailable(4200);
            using var engine = new ManagedProcess("engine",
                Path.Combine(root, "FrameWeb", ".venv", "Scripts", "python.exe"),
                Path.Combine(root, "FrameWeb"),
                ["-m", "flask", "--app", "main:app", "run", "--host", "127.0.0.1", "--port", "8080"], logs, job);
            using var frontend = new ManagedProcess("frontend",
                Path.Combine(root, "tools", "local-tools", "node_modules", "node", "bin", "node.exe"),
                Path.Combine(root, "FrameWebforJS"),
                ["node_modules/@angular/cli/bin/ng.js", "serve", "--configuration", "local", "--host", "127.0.0.1", "--port", "4200"], logs, job);

            using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            startupTimeout.CancelAfter(TimeSpan.FromMinutes(15));
            try
            {
                state.Set("starting", "解析サーバーとフロントエンドを起動しています。");
                await WaitForReady(engine, frontend, startupTimeout.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                throw new TimeoutException("15 分以内に起動が完了しませんでした。.local/logs を確認してください。");
            }

            state.Set("ready", "起動が完了しました。フロントエンドに移動します。", true, true);
            logger.LogInformation("All services ready. Frontend: http://127.0.0.1:4200/");
            await Task.WhenAny(engine.WaitForExitAsync(stoppingToken), frontend.WaitForExitAsync(stoppingToken));
            stoppingToken.ThrowIfCancellationRequested();
            var exited = engine.HasExited ? engine : frontend;
            throw new InvalidOperationException($"{exited.Name} が終了しました (exit {exited.ExitCode})。.local/logs/{exited.Name}.log を確認してください。");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            state.Set("failed", exception.Message);
            logger.LogError(exception, "Local startup failed");
        }
    }

    private async Task WaitForReady(ManagedProcess engine, ManagedProcess frontend, CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        while (true)
        {
            token.ThrowIfCancellationRequested();
            foreach (var process in new[] { engine, frontend })
                if (process.HasExited)
                    throw new InvalidOperationException($"{process.Name} の起動に失敗しました (exit {process.ExitCode})。.local/logs/{process.Name}.log を確認してください。");

            var engineReady = await IsReady(client, "http://127.0.0.1:8080/", token);
            var frontendReady = await IsReady(client, "http://127.0.0.1:4200/", token);
            if (engineReady && frontendReady) return;
            state.Set("starting", engineReady ? "解析サーバーは起動しました。フロントエンドをビルドしています。" : "解析サーバーとフロントエンドを起動しています。",
                engineReady, frontendReady);
            await Task.Delay(1000, token);
        }
    }

    private static async Task<bool> IsReady(HttpClient client, string url, CancellationToken token)
    {
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
    }

    private static void EnsurePortAvailable(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Server.ExclusiveAddressUse = true;
        try { listener.Start(); }
        catch (SocketException e)
        {
            throw new InvalidOperationException($"ポート {port} は使用中です。先に起動しているサーバーを終了してから F5 を実行してください。", e);
        }
        finally { listener.Stop(); }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "FrameWeb", "main.py")) &&
                File.Exists(Path.Combine(directory.FullName, "FrameWebforJS", "package.json")))
                return directory.FullName;
        throw new DirectoryNotFoundException("FrameWeb/main.py を含むリポジトリが見つかりません。");
    }
}
