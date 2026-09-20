using System.Text;
using FrameWeb.LocalRuntime;

namespace FrameWeb.Startup;

internal sealed class ManagedProcess : IDisposable
{
    private readonly ManagedChildProcess _process;
    private readonly StreamWriter _log;
    private readonly object _logLock = new();
    private bool _disposed;

    public ManagedProcess(
        string name,
        string executable,
        string workingDirectory,
        IEnumerable<string> arguments,
        string logDirectory,
        WindowsProcessJob job)
    {
        Name = name;
        Directory.CreateDirectory(logDirectory);
        _log = new StreamWriter(
            Path.Combine(logDirectory, name + ".log"),
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        Dictionary<string, string?> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PYTHONUTF8"] = "1",
            ["PYTHONUNBUFFERED"] = "1",
            ["NG_CLI_ANALYTICS"] = "false",
            ["ASPNETCORE_URLS"] = null,
        };

        try
        {
            _process = new ManagedChildProcess(
                new FrameWebRuntimeCommand(executable, workingDirectory, arguments, environment),
                job,
                (_, line) => Write(line));
            Write($"Started PID {_process.Id}");
        }
        catch
        {
            _log.Dispose();
            throw;
        }
    }

    public string Name { get; }

    public bool HasExited => _process.HasExited;

    public int ExitCode => _process.ExitCode
        ?? throw new InvalidOperationException($"{Name} has not exited.");

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _process.StopAsync(TimeSpan.FromSeconds(5), CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            // Closing the owning job remains the final bounded process-tree cleanup.
        }
        finally
        {
            _process.Dispose();
            lock (_logLock)
            {
                _disposed = true;
                _log.Dispose();
            }
        }
    }

    private void Write(string line)
    {
        lock (_logLock)
        {
            if (_disposed)
            {
                return;
            }

            _log.WriteLine(line);
            Console.WriteLine($"[{Name}] {line}");
        }
    }
}
