using System.Diagnostics;
using System.Text;

namespace FrameWeb.LocalRuntime;

internal sealed class ManagedChildProcess : IDisposable
{
    private const int DefaultMaximumOutputLineCharacters = 4 * 1024;
    private readonly Process _process;
    private readonly Action<FrameWebRuntimeOutputStream, string, bool> _output;
    private readonly Func<string, string>? _sanitizeOutput;
    private readonly int _maximumOutputLineCharacters;
    private int _disposed;

    public ManagedChildProcess(
        FrameWebRuntimeCommand command,
        WindowsProcessJob job,
        Action<FrameWebRuntimeOutputStream, string> output)
        : this(
            command,
            job,
            (stream, line, _) => output(stream, line),
            DefaultMaximumOutputLineCharacters,
            sanitizeOutput: null)
    {
    }

    public ManagedChildProcess(
        FrameWebRuntimeCommand command,
        WindowsProcessJob job,
        Action<FrameWebRuntimeOutputStream, string, bool> output,
        int maximumOutputLineCharacters,
        Func<string, string>? sanitizeOutput)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(output);
        if (maximumOutputLineCharacters < 256)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputLineCharacters));
        }

        _output = output;
        _sanitizeOutput = sanitizeOutput;
        _maximumOutputLineCharacters = maximumOutputLineCharacters;

        ProcessStartInfo startInfo = new(command.FileName)
        {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string name, string? value) in command.Environment)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += OnStandardOutput;
        _process.ErrorDataReceived += OnStandardError;
        try
        {
            if (!_process.Start())
            {
                throw new InvalidOperationException("The FrameWeb process did not start.");
            }

            try
            {
                job.Add(_process);
            }
            catch
            {
                TryKill();
                throw;
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public int Id => _process.Id;

    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    public int? ExitCode => HasExited ? _process.ExitCode : null;

    public Task WaitForExitAsync(CancellationToken cancellationToken) => _process.WaitForExitAsync(cancellationToken);

    public async Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (HasExited)
        {
            await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        TryKill();
        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await _process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The FrameWeb process did not stop within {timeout}.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        TryKill();
        _process.OutputDataReceived -= OnStandardOutput;
        _process.ErrorDataReceived -= OnStandardError;
        _process.Dispose();
    }

    private void OnStandardOutput(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is not null)
        {
            Publish(FrameWebRuntimeOutputStream.StandardOutput, eventArgs.Data);
        }
    }

    private void OnStandardError(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is not null)
        {
            Publish(FrameWebRuntimeOutputStream.StandardError, eventArgs.Data);
        }
    }

    private void Publish(FrameWebRuntimeOutputStream stream, string line)
    {
        string sanitized = _sanitizeOutput?.Invoke(line) ?? line;
        bool truncated = sanitized.Length > _maximumOutputLineCharacters;
        if (truncated)
        {
            const string suffix = "...[truncated]";
            sanitized = string.Concat(
                sanitized.AsSpan(0, _maximumOutputLineCharacters - suffix.Length),
                suffix);
        }

        _output(stream, sanitized, truncated);
    }

    private void TryKill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process was never started or already exited.
        }
    }
}
