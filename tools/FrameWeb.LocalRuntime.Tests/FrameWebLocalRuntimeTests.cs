using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

namespace FrameWeb.LocalRuntime.Tests;

public sealed class FrameWebLocalRuntimeTests
{
    [Fact]
    public async Task StartAndStop_ReadinessSuccessCapturesBothStreamsAndIsIdempotent()
    {
        using TemporaryRepository repository = new();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand command = RuntimeTestCommands.OwnedReadyServer(
            repository.Root,
            port,
            "[Console]::Out.WriteLine('engine-out'); [Console]::Error.WriteLine('engine-err')");
        await using FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(repository.Root, command, port));

        await runtime.StartAsync();
        await runtime.StartAsync();
        await RuntimeTestCommands.WaitUntilAsync(
            () => runtime.Diagnostics.StandardOutput.Contains("engine-out") &&
                  runtime.Diagnostics.StandardError.Contains("engine-err"),
            TimeSpan.FromSeconds(3));
        int processId = Assert.IsType<int>(runtime.ProcessId);

        Assert.Equal(FrameWebRuntimeState.Ready, runtime.State);
        Assert.True(RuntimeTestCommands.IsProcessAlive(processId));
        using HttpClient authenticatedClient = runtime.CreateHttpClient();
        Assert.Empty(authenticatedClient.DefaultRequestHeaders);
        using HttpRequestMessage callerRequest = new(HttpMethod.Get, "/");
        using HttpResponseMessage authenticatedResponse = await authenticatedClient.SendAsync(callerRequest);
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
        Assert.False(callerRequest.Headers.Contains(FrameWebLocalAuthentication.HeaderName));
        Assert.False(authenticatedResponse.RequestMessage!.Headers.Contains(FrameWebLocalAuthentication.HeaderName));

        await runtime.StopAsync();
        await runtime.StopAsync();

        Assert.Equal(FrameWebRuntimeState.Stopped, runtime.State);
        Assert.False(RuntimeTestCommands.IsProcessAlive(processId));

        await runtime.StartAsync();
        int restartedProcessId = Assert.IsType<int>(runtime.ProcessId);
        await runtime.StopAsync();
        Assert.False(RuntimeTestCommands.IsProcessAlive(restartedProcessId));
    }

    [Fact]
    public async Task Start_ProcessExitBeforeReadinessReturnsDiagnostics()
    {
        using TemporaryRepository repository = new();
        int unavailablePort = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand command = RuntimeTestCommands.PowerShell(
            repository.Root,
            "[Console]::Out.WriteLine('before-exit'); [Console]::Error.WriteLine('fatal-detail'); exit 7");
        await using FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(repository.Root, command, unavailablePort));

        FrameWebRuntimeException exception = await Assert.ThrowsAsync<FrameWebRuntimeException>(
            () => runtime.StartAsync());
        await RuntimeTestCommands.WaitUntilAsync(
            () => runtime.Diagnostics.StandardError.Contains("fatal-detail"),
            TimeSpan.FromSeconds(2));

        Assert.Contains("exit 7", exception.Message, StringComparison.Ordinal);
        Assert.Equal(FrameWebRuntimeState.Faulted, runtime.State);
        Assert.Equal(7, runtime.Diagnostics.ExitCode);
        Assert.Contains("before-exit", runtime.Diagnostics.StandardOutput);
    }

    [Fact]
    public async Task Start_TimeoutAndCancellationKillOwnedProcess()
    {
        using TemporaryRepository repository = new();
        int unavailablePort = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand command = RuntimeTestCommands.PowerShell(
            repository.Root,
            "while ($true) { Start-Sleep -Milliseconds 100 }");

        await using (FrameWebLocalRuntime timedOut = new(RuntimeTestCommands.Options(
            repository.Root,
            command,
            unavailablePort,
            TimeSpan.FromMilliseconds(350))))
        {
            await Assert.ThrowsAsync<TimeoutException>(() => timedOut.StartAsync());
            int processId = Assert.IsType<int>(timedOut.ProcessId);
            Assert.Equal(FrameWebRuntimeState.Faulted, timedOut.State);
            Assert.False(RuntimeTestCommands.IsProcessAlive(processId));
        }

        await using FrameWebLocalRuntime cancelled = new(RuntimeTestCommands.Options(
            repository.Root,
            command,
            unavailablePort,
            TimeSpan.FromSeconds(5)));
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.StartAsync(cancellation.Token));
        int cancelledProcessId = Assert.IsType<int>(cancelled.ProcessId);
        Assert.Equal(FrameWebRuntimeState.Stopped, cancelled.State);
        Assert.False(RuntimeTestCommands.IsProcessAlive(cancelledProcessId));
    }

    [Fact]
    public async Task OutputCapture_IsBoundedAndReportsTruncation()
    {
        using TemporaryRepository repository = new();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand command = RuntimeTestCommands.OwnedReadyServer(
            repository.Root,
            port,
            "[Console]::Out.WriteLine($env:FRAMEWEB_LOCAL_AUTH_TOKEN); " +
            "[Console]::Out.WriteLine(('x' * 5000)); " +
            "1..200 | ForEach-Object { [Console]::Out.WriteLine(('line-{0:D3}-' -f $_) + ('x' * 40)) }");
        ConcurrentQueue<FrameWebRuntimeOutputEventArgs> events = new();
        await using FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(
            repository.Root,
            command,
            port,
            outputBudget: 1024,
            maximumOutputLineCharacters: 512));
        runtime.OutputReceived += (_, output) => events.Enqueue(output);

        await runtime.StartAsync();
        await RuntimeTestCommands.WaitUntilAsync(
            () => runtime.Diagnostics.OutputTruncated && events.Any(output => output.IsTruncated),
            TimeSpan.FromSeconds(3));

        FrameWebRuntimeDiagnostics diagnostics = runtime.Diagnostics;
        Assert.True(diagnostics.OutputTruncated);
        Assert.True(diagnostics.StandardOutput.Sum(line => line.Length + 1) <= 1024);
        Assert.Contains(events, output => output.Line == "[REDACTED]");
        Assert.All(events, output => Assert.InRange(output.Line.Length, 0, 512));
        Assert.DoesNotContain(events, output => output.Line.Length == 43 &&
            output.Line.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
    }

    [Fact]
    public async Task Start_UnrelatedPortSquatterNeverReceivesTokenAndOwnedOversizedBodyIsRejected()
    {
        using TemporaryRepository repository = new();
        FrameWebRuntimeCommand idleCommand = RuntimeTestCommands.PowerShell(
            repository.Root,
            "while ($true) { Start-Sleep -Milliseconds 100 }");

        await using (LocalReadyServer unrelated = new(body: LocalReadyServer.ReadyMarker))
        await using (FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(
            repository.Root,
            idleCommand,
            unrelated.Port,
            TimeSpan.FromMilliseconds(350))))
        {
            await Assert.ThrowsAsync<TimeoutException>(() => runtime.StartAsync());
            Assert.Equal(FrameWebRuntimeState.Faulted, runtime.State);
            Assert.Empty(unrelated.ReceivedTokens);
            Assert.False(RuntimeTestCommands.IsProcessAlive(Assert.IsType<int>(runtime.ProcessId)));
        }

        int oversizedPort = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand oversizedCommand = RuntimeTestCommands.OwnedReadyServer(
            repository.Root,
            oversizedPort,
            responseBody: new string('x', 2048));
        await using (FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(
            repository.Root,
            oversizedCommand,
            oversizedPort,
            TimeSpan.FromMilliseconds(350))))
        {
            await Assert.ThrowsAsync<TimeoutException>(() => runtime.StartAsync());
            Assert.Equal(FrameWebRuntimeState.Faulted, runtime.State);
        }

        int wrongMarkerPort = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand wrongMarkerCommand = RuntimeTestCommands.OwnedReadyServer(
            repository.Root,
            wrongMarkerPort,
            responseBody: "{\"status\":\"ready\"}");
        await using (FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(
            repository.Root,
            wrongMarkerCommand,
            wrongMarkerPort,
            TimeSpan.FromMilliseconds(350))))
        {
            await Assert.ThrowsAsync<TimeoutException>(() => runtime.StartAsync());
            Assert.Equal(FrameWebRuntimeState.Faulted, runtime.State);
        }
    }

    [Fact]
    public async Task AnalysisRequest_OwnershipLossPreventsTokenAndBodyDisclosure()
    {
        using TemporaryRepository repository = new();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand command = RuntimeTestCommands.OwnedReadyServer(
            repository.Root,
            port,
            maximumRequests: 1);
        await using FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(repository.Root, command, port));
        await runtime.StartAsync();
        using HttpClient client = runtime.CreateHttpClient();
        await RuntimeTestCommands.WaitUntilAsync(
            () => runtime.Diagnostics.StandardOutput.Contains("LISTENER_STOPPED"),
            TimeSpan.FromSeconds(3));

        await using LocalReadyServer squatter = new(port: port);
        using ByteArrayContent body = new("secret-body"u8.ToArray());
        FrameWebRuntimeException exception = await Assert.ThrowsAsync<FrameWebRuntimeException>(
            () => client.PostAsync("/", body));

        Assert.Contains("not owned", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(squatter.ReceivedTokens);
    }

    [Fact]
    public async Task AnalysisRequest_DoesNotFollowRedirectOrForwardToken()
    {
        using TemporaryRepository repository = new();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        await using LocalReadyServer redirectTarget = new();
        FrameWebRuntimeCommand command = RuntimeTestCommands.OwnedReadyServer(
            repository.Root,
            port,
            redirectAfterReadiness: new Uri($"http://127.0.0.1:{redirectTarget.Port}/"));
        await using FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(repository.Root, command, port));
        await runtime.StartAsync();
        using HttpClient client = runtime.CreateHttpClient();

        using HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(redirectTarget.ReceivedTokens);
        Assert.False(response.RequestMessage!.Headers.Contains(FrameWebLocalAuthentication.HeaderName));
    }

    [Fact]
    public async Task Start_OwnedListenerTokenMismatchNeverEstablishesReadiness()
    {
        using TemporaryRepository repository = new();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand command = RuntimeTestCommands.OwnedReadyServer(
            repository.Root,
            port,
            acceptRuntimeToken: false);
        await using FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(
            repository.Root,
            command,
            port,
            TimeSpan.FromMilliseconds(350)));

        await Assert.ThrowsAsync<TimeoutException>(() => runtime.StartAsync());

        Assert.Equal(FrameWebRuntimeState.Faulted, runtime.State);
        Assert.False(RuntimeTestCommands.IsProcessAlive(Assert.IsType<int>(runtime.ProcessId)));
    }

    [Fact]
    public async Task Stop_ClosesJobAndLeavesNoSurvivingChildProcess()
    {
        using TemporaryRepository repository = new();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        string childScript = "Start-Sleep -Seconds 120";
        string parentScript =
            "$child = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') " +
            "-ArgumentList @('-NoProfile','-NonInteractive','-Command','" + childScript + "') " +
            "-WindowStyle Hidden -PassThru; " +
            "[Console]::Out.WriteLine(('CHILD_PID=' + $child.Id))";
        FrameWebRuntimeCommand command = RuntimeTestCommands.OwnedReadyServer(repository.Root, port, parentScript);
        await using FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(repository.Root, command, port));

        await runtime.StartAsync();
        await RuntimeTestCommands.WaitUntilAsync(
            () => runtime.Diagnostics.StandardOutput.Any(line => line.StartsWith("CHILD_PID=", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(5));
        string childLine = runtime.Diagnostics.StandardOutput.Single(line => line.StartsWith("CHILD_PID=", StringComparison.Ordinal));
        int childProcessId = int.Parse(childLine["CHILD_PID=".Length..], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(RuntimeTestCommands.IsProcessAlive(childProcessId));

        await runtime.StopAsync();
        await RuntimeTestCommands.WaitUntilAsync(
            () => !RuntimeTestCommands.IsProcessAlive(childProcessId),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Dispose_IsIdempotentAndKillsRunningProcess()
    {
        using TemporaryRepository repository = new();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeCommand command = RuntimeTestCommands.OwnedReadyServer(repository.Root, port);
        FrameWebLocalRuntime runtime = new(RuntimeTestCommands.Options(repository.Root, command, port));

        await runtime.StartAsync();
        int processId = Assert.IsType<int>(runtime.ProcessId);

        runtime.Dispose();
        runtime.Dispose();
        await runtime.DisposeAsync();

        Assert.Equal(FrameWebRuntimeState.Disposed, runtime.State);
        Assert.False(RuntimeTestCommands.IsProcessAlive(processId));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => runtime.StartAsync());
    }

}
