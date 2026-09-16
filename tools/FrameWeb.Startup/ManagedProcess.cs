using System.Diagnostics;
using System.Text;

namespace FrameWeb.Startup;

internal sealed class ManagedProcess : IDisposable
{
    private readonly Process process;
    private readonly StreamWriter log;
    private readonly object logLock = new();
    private bool disposed;
    public string Name { get; }
    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;

    public ManagedProcess(string name, string executable, string workingDirectory,
        IEnumerable<string> arguments, string logDirectory, WindowsProcessJob job)
    {
        Name = name;
        Directory.CreateDirectory(logDirectory);
        log = new StreamWriter(Path.Combine(logDirectory, name + ".log"), false, new UTF8Encoding(false))
            { AutoFlush = true };
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["PYTHONUNBUFFERED"] = "1";
        info.Environment["NG_CLI_ANALYTICS"] = "false";
        // Visual Studio/ASP.NET debugging settings belong to the .NET host only.
        info.Environment.Remove("ASPNETCORE_URLS");
        process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) => Write(e.Data);
        process.ErrorDataReceived += (_, e) => Write(e.Data);
        try
        {
            process.Start();
            job.Add(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Write($"Started PID {process.Id}");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public Task WaitForExitAsync(CancellationToken token) => process.WaitForExitAsync(token);

    private void Write(string? line)
    {
        if (line is null) return;
        lock (logLock)
        {
            if (disposed) return;
            log.WriteLine(line);
            Console.WriteLine($"[{Name}] {line}");
        }
    }

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException) { /* Process did not start or already exited. */ }
        finally
        {
            lock (logLock)
            {
                disposed = true;
                log.Dispose();
            }
            process.Dispose();
        }
    }
}
