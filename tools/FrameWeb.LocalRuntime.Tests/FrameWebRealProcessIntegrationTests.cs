using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace FrameWeb.LocalRuntime.Tests;

public sealed class FrameWebRealProcessIntegrationTests
{
    [Fact]
    public async Task RealUvFlask_AnalyzesRepresentativeFrameAndDisposalKillsPythonTree()
    {
        string repositoryRoot = RuntimeTestCommands.FindRepositoryRoot();
        int port = RuntimeTestCommands.ReserveAndReleasePort();
        FrameWebRuntimeOptions options = new(repositoryRoot)
        {
            Port = port,
            CommandOverride = CreateUvFlaskCommand(repositoryRoot, port),
            StartupTimeout = TimeSpan.FromSeconds(60),
            StopTimeout = TimeSpan.FromSeconds(10),
            PollInterval = TimeSpan.FromMilliseconds(50),
            ReadinessRequestTimeout = TimeSpan.FromSeconds(2),
        };
        FrameWebLocalRuntime runtime = new(options);
        int[] ownedProcessIds = [];
        try
        {
            await runtime.StartAsync().WaitAsync(TimeSpan.FromSeconds(75));
            Assert.Equal(FrameWebRuntimeState.Ready, runtime.State);

            await RuntimeTestCommands.WaitUntilAsync(
                () => runtime.SnapshotOwnedProcessIds().Any(IsPythonProcess),
                TimeSpan.FromSeconds(10));
            ownedProcessIds = runtime.SnapshotOwnedProcessIds();
            Assert.Contains(ownedProcessIds, processId => processId == runtime.ProcessId);
            Assert.Contains(ownedProcessIds, IsPythonProcess);

            using HttpClient wrongTokenClient = new() { BaseAddress = options.EffectiveReadinessUri };
            wrongTokenClient.DefaultRequestHeaders.TryAddWithoutValidation(
                FrameWebLocalAuthentication.HeaderName,
                "wrong-token");
            using ByteArrayContent wrongBody = new("{}"u8.ToArray());
            wrongBody.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            using HttpResponseMessage unauthorized = await wrongTokenClient.PostAsync("/", wrongBody)
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

            using HttpClient analysisHttpClient = runtime.CreateHttpClient();
            FrameWebAnalysisClient analysisClient = new(analysisHttpClient);

            AnalysisResultSet result = await analysisClient.AnalyzeAsync(
                ProjectDocumentPresets.CreateRepresentativeFrame()).WaitAsync(TimeSpan.FromSeconds(60));

            Assert.Equal(AnalysisResultSet.ContractKind, result.Kind);
            Assert.Equal(AnalysisResultSet.ContractVersion, result.SchemaVersion);

            AnalysisCase resultCase = Assert.Single(result.Cases);
            Assert.Equal("1", resultCase.CaseId);
            Assert.Equal(AnalysisType.Static, resultCase.AnalysisType);
            Assert.Equal(["1"], resultCase.SupportNodeIds);
            Assert.Equal(["1", "2"], result.Topology.Nodes.Select(node => node.NodeId));

            TopologyMember topologyMember = Assert.Single(result.Topology.Members);
            Assert.Equal("1", topologyMember.MemberId);
            Assert.Equal("1", topologyMember.NodeI);
            Assert.Equal("2", topologyMember.NodeJ);

            StaticAnalysisResult staticResult = Assert.IsType<StaticAnalysisResult>(Assert.Single(result.Results));
            Assert.Equal("1", staticResult.CaseId);
            Assert.Equal(ResultStateKind.Static, staticResult.State.Kind);
            Assert.Equal(["1", "2"], staticResult.NodeDisplacements.Select(value => value.NodeId));
            NodeDisplacement loadedNode = Assert.Single(
                staticResult.NodeDisplacements,
                displacement => displacement.NodeId == "2");
            Assert.True(double.IsFinite(loadedNode.Components.Dy));
            Assert.True(
                loadedNode.Components.Dy < -1e-12,
                $"Expected loaded node 2 to displace in -Y, got {loadedNode.Components.Dy:R}.");

            SupportReaction supportReaction = Assert.Single(staticResult.SupportReactions);
            Assert.Equal("1", supportReaction.NodeId);
            Assert.InRange(supportReaction.Components.Fy, 10.0 - 1e-7, 10.0 + 1e-7);

            MemberSectionForces memberForces = Assert.Single(staticResult.MemberSectionForces);
            Assert.Equal("1", memberForces.MemberId);
            Assert.NotEmpty(memberForces.Segments);
            Assert.All(memberForces.Segments, segment =>
            {
                Assert.True(double.IsFinite(segment.Length) && segment.Length > 0.0);
                AssertFinite(segment.IEnd);
                AssertFinite(segment.JEnd);
            });
            Assert.Contains(memberForces.Segments, segment =>
                HasNonZeroComponent(segment.IEnd) || HasNonZeroComponent(segment.JEnd));
        }
        finally
        {
            ownedProcessIds = ownedProcessIds.Union(runtime.SnapshotOwnedProcessIds()).ToArray();
            await runtime.DisposeAsync();
        }

        Assert.NotEmpty(ownedProcessIds);
        await RuntimeTestCommands.WaitUntilAsync(
            () => ownedProcessIds.All(processId => !RuntimeTestCommands.IsProcessAlive(processId)),
            TimeSpan.FromSeconds(10));
    }

    private static bool IsPythonProcess(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.ProcessName.StartsWith("python", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void AssertFinite(ForceComponents value)
    {
        Assert.All(
            new[] { value.Fx, value.Fy, value.Fz, value.Mx, value.My, value.Mz },
            component => Assert.True(double.IsFinite(component)));
    }

    private static bool HasNonZeroComponent(ForceComponents value) =>
        new[] { value.Fx, value.Fy, value.Fz, value.Mx, value.My, value.Mz }
            .Any(component => Math.Abs(component) > 1e-12);

    private static FrameWebRuntimeCommand CreateUvFlaskCommand(string repositoryRoot, int port)
    {
        string frameWebDirectory = Path.Combine(repositoryRoot, "FrameWeb");
        return new FrameWebRuntimeCommand(
            "uv",
            repositoryRoot,
            [
                "--directory", frameWebDirectory,
                "run", "--locked", "python",
                "-m", "flask", "--app", "main:app", "run",
                "--host", "127.0.0.1",
                "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ],
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["PYTHONUTF8"] = "1",
                ["PYTHONUNBUFFERED"] = "1",
            });
    }
}
