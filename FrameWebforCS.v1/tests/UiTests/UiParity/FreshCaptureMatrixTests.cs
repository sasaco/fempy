using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PDF_Manager.Shell;
using Xunit.Abstractions;

namespace PDF_Manager.UiTests.UiParity;

public sealed class FreshCaptureMatrixTests(ITestOutputHelper output)
{
    private static readonly (Size LogicalSize, Size ImageSize, int DpiPercent)[] RequiredConfigurations =
    [
        (new Size(1200, 800), new Size(1200, 800), 100),
        (new Size(1024, 768), new Size(1024, 768), 100),
        (new Size(1440, 900), new Size(2160, 1350), 150),
    ];

    [Fact]
    public async Task FreshCurrentBuildMatrix_CoversSourceManifestAndMeetsAllAvailableAngularReferences()
    {
        string outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"frameweb-current-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            string probePath = LiveCaptureProbePath();
            Assert.True(File.Exists(probePath), $"LiveCaptureProbe was not built: {probePath}");
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                },
            };
            process.StartInfo.ArgumentList.Add(probePath);
            process.StartInfo.ArgumentList.Add(outputDirectory);
            process.StartInfo.ArgumentList.Add("--matrix");
            Assert.True(process.Start(), "LiveCaptureProbe did not start.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("LiveCaptureProbe matrix did not finish within three minutes.");
            }

            string stdout = await standardOutput;
            string stderr = await standardError;
            output.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr)) output.WriteLine(stderr);
            Assert.True(
                process.ExitCode == 0,
                $"LiveCaptureProbe exited with {process.ExitCode}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");

            string metadataPath = Path.Combine(outputDirectory, "winforms-live-captures.v2.json");
            JsonObject liveMetadata = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(metadataPath)));
            Assert.Equal(2, liveMetadata["manifestVersion"]!.GetValue<int>());
            Assert.Equal("ja", liveMetadata["language"]!.GetValue<string>());
            Assert.Contains("source-derived", liveMetadata["structuralGate"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal(
                AssemblyHash(typeof(MainForm).Assembly),
                liveMetadata["productAssemblySha256"]!.GetValue<string>());
            Assert.Equal(
                FileHash(probePath),
                liveMetadata["probeAssemblySha256"]!.GetValue<string>());

            LiveCapture[] current = Assert.IsType<JsonArray>(liveMetadata["captures"])
                .Select(ParseLiveCapture)
                .ToArray();
            AssertCompleteSourceAndManifestMatrix(current, outputDirectory);
            CompareEveryAvailableAngularReference(current, outputDirectory);
        }
        finally
        {
            string fullOutput = Path.GetFullPath(outputDirectory);
            string fullTemp = Path.GetFullPath(Path.GetTempPath());
            if (fullOutput.StartsWith(fullTemp, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(fullOutput).StartsWith("frameweb-current-capture-", StringComparison.Ordinal))
            {
                Directory.Delete(fullOutput, recursive: true);
            }
        }
    }

    private static void AssertCompleteSourceAndManifestMatrix(
        IReadOnlyCollection<LiveCapture> captures,
        string outputDirectory)
    {
        AngularSourceInventory angular = new AngularSourceInventoryExtractor(ParityTestPaths.RepositoryRoot).Extract();
        JsonObject manifest = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(ParityTestPaths.ManifestPath)));
        string[] overlayStates = Assert.IsType<JsonArray>(manifest["overlays"])
            .OfType<JsonObject>()
            .Select(overlay => $"overlay.{overlay["kind"]!.GetValue<string>()}")
            .ToArray();
        string[] expectedStates =
        [
            "shell.empty",
            .. angular.Routes.Select(route => $"route.{route.AngularRoute}"),
            .. overlayStates,
        ];

        Assert.Equal(23, angular.Routes.Count);
        Assert.Equal(6, overlayStates.Length);
        Assert.Equal(30, expectedStates.Length); // 29 route/overlay states plus the empty shared shell.
        Assert.Equal(expectedStates.Length, expectedStates.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expectedStates.Length * RequiredConfigurations.Length, captures.Count);

        foreach ((Size logicalSize, Size imageSize, int dpiPercent) in RequiredConfigurations)
        {
            LiveCapture[] configuration = captures
                .Where(capture => capture.LogicalSize == logicalSize && capture.DpiPercent == dpiPercent)
                .ToArray();
            Assert.Equal(expectedStates.Length, configuration.Length);
            Assert.Equal(
                expectedStates.OrderBy(state => state, StringComparer.Ordinal),
                configuration.Select(capture => capture.State).OrderBy(state => state, StringComparer.Ordinal));
            Assert.All(configuration, capture =>
            {
                Assert.Equal(imageSize, capture.ImageSize);
                Assert.Equal(dpiPercent / 100d, capture.RenderScale, 6);
                string path = Path.Combine(outputDirectory, capture.File);
                Assert.True(File.Exists(path), $"Fresh matrix image is missing: {path}");
                using Bitmap bitmap = new(path);
                Assert.Equal(imageSize, bitmap.Size);
                Assert.Equal(capture.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
                if (capture.State.StartsWith("route.", StringComparison.Ordinal))
                {
                    Assert.False(string.IsNullOrWhiteSpace(capture.RouteId));
                }
                else
                {
                    Assert.Null(capture.RouteId);
                }
            });
        }

        string[] unexpectedConfigurations = captures
            .Where(capture => !RequiredConfigurations.Any(required =>
                required.LogicalSize == capture.LogicalSize &&
                required.ImageSize == capture.ImageSize &&
                required.DpiPercent == capture.DpiPercent))
            .Select(capture => $"{capture.State}@{capture.LogicalSize}/{capture.DpiPercent}")
            .ToArray();
        Assert.True(
            unexpectedConfigurations.Length == 0,
            $"Fresh matrix contains unexpected configurations: {string.Join(", ", unexpectedConfigurations)}");
    }

    private void CompareEveryAvailableAngularReference(
        IReadOnlyCollection<LiveCapture> current,
        string outputDirectory)
    {
        JsonObject angularMetadata = Assert.IsType<JsonObject>(
            JsonNode.Parse(File.ReadAllText(ParityTestPaths.ReferenceMetadataPath)));
        JsonObject policy = Assert.IsType<JsonObject>(angularMetadata["comparisonPolicy"]);
        int maxChannelDelta = policy["maxPerChannelDelta"]!.GetValue<int>();
        double maxMismatchRatio = policy["maxOverThresholdPixelRatio"]!.GetValue<double>();
        int edgeMaskRadius = policy["edgeAntialiasingMaskRadiusPhysicalPixels"]!.GetValue<int>();
        AngularCapture[] references = Assert.IsType<JsonArray>(angularMetadata["captures"])
            .Select(ParseAngularCapture)
            .ToArray();
        Assert.NotEmpty(references);
        List<string> violations = [];

        foreach (AngularCapture reference in references)
        {
            LiveCapture actual = Assert.Single(
                current,
                capture => capture.State == reference.State &&
                           capture.LogicalSize == reference.LogicalSize &&
                           capture.ImageSize == reference.ImageSize &&
                           capture.DpiPercent == reference.DpiPercent);
            VisualParityDifference difference = VisualParityComparator.Compare(
                Path.Combine(Path.GetDirectoryName(ParityTestPaths.ReferenceMetadataPath)!, reference.File),
                Path.Combine(outputDirectory, actual.File),
                RegionsFor(reference),
                maxChannelDelta,
                edgeMaskRadius);
            output.WriteLine(
                $"fresh {reference.State}@{reference.LogicalSize}/{reference.DpiPercent}: " +
                $"ratio={difference.OverThresholdPixelRatio:F6}, mae={difference.MeanAbsoluteChannelError:F4}, " +
                $"compared={difference.ComparedPixels}, masked={difference.EdgeMaskedPixels}");
            try
            {
                VisualParityComparator.RequireWithinThreshold(difference, maxMismatchRatio);
            }
            catch (InvalidDataException exception)
            {
                violations.Add(
                    $"{reference.State}@{reference.LogicalSize}/{reference.DpiPercent}: {exception.Message}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Fresh current-build captures exceed the pinned Angular threshold:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static IReadOnlyList<Rectangle> RegionsFor(AngularCapture capture)
    {
        double scale = capture.DpiPercent / 100d;
        List<Rectangle> logical =
        [
            new Rectangle(0, 0, capture.LogicalSize.Width, 84),
            new Rectangle(0, 84, 124, capture.LogicalSize.Height - 84),
        ];
        if (capture.State == "overlay.start")
        {
            logical.Add(new Rectangle(
                (capture.LogicalSize.Width - 760) / 2,
                (capture.LogicalSize.Height - 400) / 2,
                760,
                195));
        }
        else if (capture.State.StartsWith("route.", StringComparison.Ordinal))
        {
            logical.Add(new Rectangle(
                capture.LogicalSize.Width - 430,
                84,
                430,
                capture.LogicalSize.Height - 84));
        }

        return logical.Select(region => Scale(region, scale)).ToArray();
    }

    private static Rectangle Scale(Rectangle rectangle, double scale) => new(
        (int)Math.Round(rectangle.X * scale),
        (int)Math.Round(rectangle.Y * scale),
        (int)Math.Round(rectangle.Width * scale),
        (int)Math.Round(rectangle.Height * scale));

    private static LiveCapture ParseLiveCapture(JsonNode? node)
    {
        JsonObject capture = Assert.IsType<JsonObject>(node);
        return new LiveCapture(
            capture["state"]!.GetValue<string>(),
            capture["file"]!.GetValue<string>(),
            new Size(capture["logicalWidth"]!.GetValue<int>(), capture["logicalHeight"]!.GetValue<int>()),
            new Size(capture["imageWidth"]!.GetValue<int>(), capture["imageHeight"]!.GetValue<int>()),
            capture["dpiPercent"]!.GetValue<int>(),
            capture["sha256"]!.GetValue<string>(),
            capture["renderScale"]!.GetValue<double>(),
            capture["routeId"]?.GetValue<string>(),
            capture["dimension"]!.GetValue<string>());
    }

    private static AngularCapture ParseAngularCapture(JsonNode? node)
    {
        JsonObject capture = Assert.IsType<JsonObject>(node);
        return new AngularCapture(
            capture["state"]!.GetValue<string>(),
            capture["file"]!.GetValue<string>(),
            new Size(capture["logicalWidth"]!.GetValue<int>(), capture["logicalHeight"]!.GetValue<int>()),
            new Size(capture["imageWidth"]!.GetValue<int>(), capture["imageHeight"]!.GetValue<int>()),
            capture["dpiPercent"]!.GetValue<int>());
    }

    private static string AssemblyHash(Assembly assembly) =>
        FileHash(assembly.Location);

    private static string FileHash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string LiveCaptureProbePath()
    {
        DirectoryInfo targetFramework = new(Path.GetDirectoryName(typeof(FreshCaptureMatrixTests).Assembly.Location)!);
        string configuration = targetFramework.Parent?.Name
            ?? throw new DirectoryNotFoundException("Could not resolve the active test configuration.");
        return Path.Combine(
            ParityTestPaths.RepositoryRoot,
            "FramePrintPDF",
            "PDF_Manager.UiTests",
            "LiveCaptureProbe",
            "bin",
            configuration,
            "net8.0-windows",
            "LiveCaptureProbe.dll");
    }

    private sealed record LiveCapture(
        string State,
        string File,
        Size LogicalSize,
        Size ImageSize,
        int DpiPercent,
        string Sha256,
        double RenderScale,
        string? RouteId,
        string Dimension);

    private sealed record AngularCapture(
        string State,
        string File,
        Size LogicalSize,
        Size ImageSize,
        int DpiPercent);
}
