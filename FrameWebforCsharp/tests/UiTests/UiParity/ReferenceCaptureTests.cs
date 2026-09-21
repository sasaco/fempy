using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace PDF_Manager.UiTests.UiParity;

public sealed class ReferenceCaptureTests(ITestOutputHelper output)
{
    [Fact]
    public void AngularReferenceSet_HasRequiredStatesSizesDpiAndDistinctHashes()
    {
        JsonObject metadata = LoadMetadata();
        JsonArray captures = Assert.IsType<JsonArray>(metadata["captures"]);
        Capture[] parsed = captures.Select(ParseCapture).ToArray();

        Assert.Contains(parsed, capture => capture.State == "shell.empty" && capture.LogicalSize == new Size(1200, 800) && capture.DpiPercent == 100);
        Assert.Contains(parsed, capture => capture.State == "overlay.start" && capture.LogicalSize == new Size(1200, 800) && capture.DpiPercent == 100);
        Assert.Contains(parsed, capture => capture.State == "route.input-elements" && capture.LogicalSize == new Size(1200, 800) && capture.DpiPercent == 100);
        Assert.Contains(parsed, capture => capture.State == "route.input-nodes" && capture.LogicalSize == new Size(1200, 800) && capture.DpiPercent == 100);
        Assert.Contains(parsed, capture => capture.State == "route.input-fix_nodes" && capture.LogicalSize == new Size(1200, 800) && capture.DpiPercent == 100);
        Assert.Contains(parsed, capture => capture.State == "shell.empty" && capture.LogicalSize == new Size(1024, 768) && capture.DpiPercent == 100);
        Assert.Contains(parsed, capture => capture.State == "shell.empty" && capture.LogicalSize == new Size(1440, 900) && capture.DpiPercent == 150);

        Capture[] representative = parsed.Where(capture => capture.LogicalSize == new Size(1200, 800)).ToArray();
        Assert.Equal(representative.Length, representative.Select(capture => capture.Sha256).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AngularReferenceSet_FilesMatchPinnedHashesAndPixelDimensions()
    {
        JsonArray captures = Assert.IsType<JsonArray>(LoadMetadata()["captures"]);
        foreach (Capture capture in captures.Select(ParseCapture))
        {
            string path = Path.Combine(Path.GetDirectoryName(ParityTestPaths.ReferenceMetadataPath)!, capture.File);
            Assert.True(File.Exists(path), $"Missing reference image: {path}");
            using Bitmap bitmap = new(path);
            Assert.Equal(capture.ImageSize, bitmap.Size);
            string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.Equal(capture.Sha256, actualHash);
            Assert.True(new FileInfo(path).Length > 16_000, $"Reference image is suspiciously small: {capture.File}");
        }
    }

    [Fact]
    public void ComparisonPolicy_IsBoundedAndRequiresIndependentHumanApproval()
    {
        JsonObject policy = Assert.IsType<JsonObject>(LoadMetadata()["comparisonPolicy"]);
        Assert.Equal(8, policy["maxPerChannelDelta"]!.GetValue<int>());
        Assert.Equal(0.005, policy["maxOverThresholdPixelRatio"]!.GetValue<double>());
        Assert.Equal(1, policy["edgeAntialiasingMaskRadiusPhysicalPixels"]!.GetValue<int>());
        Assert.Equal(
            VisualParityComparator.MinimumUnmaskedPixelRatio,
            policy["minimumUnmaskedPixelRatioPerRegion"]!.GetValue<double>());
        Assert.True(policy["viewportPixelsExcluded"]!.GetValue<bool>());
        Assert.Contains("OpenGL", policy["regionPolicy"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(policy["humanApprovalRequired"]!.GetValue<bool>());
        Assert.False(policy["selfGeneratedCSharpGoldenAllowed"]!.GetValue<bool>());
    }

    [Fact]
    public void AngularReferenceSet_RecordsFixtureThemeLanguageAndCaptureTool()
    {
        JsonObject metadata = LoadMetadata();
        Assert.Equal(1, metadata["manifestVersion"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(metadata["fixture"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(metadata["theme"]!.GetValue<string>()));
        Assert.Equal("ja", metadata["language"]!.GetValue<string>());
        JsonObject tool = Assert.IsType<JsonObject>(metadata["tool"]);
        Assert.Contains("Chrome", tool["name"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.EndsWith("capture-angular-reference.mjs", tool["script"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void WinFormsReferenceSet_HasPinnedIndependentStateEvidence()
    {
        JsonObject metadata = LoadMetadata(ParityTestPaths.WinFormsMetadataPath);
        Assert.Equal(1, metadata["manifestVersion"]!.GetValue<int>());
        Assert.Equal("ja", metadata["language"]!.GetValue<string>());
        JsonObject tool = Assert.IsType<JsonObject>(metadata["tool"]);
        Assert.Equal("PDF_Manager.LiveCaptureProbe", tool["name"]!.GetValue<string>());
        Assert.Contains("z-order", tool["structuralGate"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        Capture[] captures = Assert.IsType<JsonArray>(metadata["captures"]).Select(ParseCapture).ToArray();
        Assert.Equal(new[] { "overlay.start", "shell.empty", "route.input-elements" }, captures.Select(capture => capture.State));
        Assert.All(captures, capture =>
        {
            Assert.Equal(new Size(1200, 800), capture.LogicalSize);
            Assert.Equal(capture.LogicalSize, capture.ImageSize);
            Assert.Equal(100, capture.DpiPercent);
            string path = Path.Combine(Path.GetDirectoryName(ParityTestPaths.WinFormsMetadataPath)!, capture.File);
            Assert.True(File.Exists(path), $"Missing WinForms evidence image: {path}");
            Assert.Equal(capture.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        });
        Assert.Equal(captures.Length, captures.Select(capture => capture.Sha256).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void WinFormsRepresentativeCaptures_MeetPinnedAngularThresholds()
    {
        JsonObject angularMetadata = LoadMetadata();
        JsonObject policy = Assert.IsType<JsonObject>(angularMetadata["comparisonPolicy"]);
        int maxChannelDelta = policy["maxPerChannelDelta"]!.GetValue<int>();
        double maxMismatchRatio = policy["maxOverThresholdPixelRatio"]!.GetValue<double>();
        int edgeMaskRadius = policy["edgeAntialiasingMaskRadiusPhysicalPixels"]!.GetValue<int>();
        Capture[] angular = Assert.IsType<JsonArray>(angularMetadata["captures"]).Select(ParseCapture).ToArray();
        Capture[] desktop = Assert.IsType<JsonArray>(LoadMetadata(ParityTestPaths.WinFormsMetadataPath)["captures"])
            .Select(ParseCapture)
            .ToArray();
        List<string> metrics = [];
        List<string> violations = [];

        foreach (Capture actual in desktop)
        {
            Capture expected = Assert.Single(
                angular,
                capture => capture.State == actual.State && capture.LogicalSize == actual.LogicalSize && capture.DpiPercent == actual.DpiPercent);
            string expectedPath = Path.Combine(Path.GetDirectoryName(ParityTestPaths.ReferenceMetadataPath)!, expected.File);
            string actualPath = Path.Combine(Path.GetDirectoryName(ParityTestPaths.WinFormsMetadataPath)!, actual.File);
            VisualParityDifference difference = VisualParityComparator.Compare(
                expectedPath,
                actualPath,
                RegionsForState(actual.State),
                maxChannelDelta,
                edgeMaskRadius);
            metrics.Add(
                $"{actual.State}: meanAbsoluteChannelError={difference.MeanAbsoluteChannelError:F4}, " +
                $"overThresholdPixelRatio={difference.OverThresholdPixelRatio:F6}, " +
                $"edgeMaskedPixels={difference.EdgeMaskedPixels}, comparedPixels={difference.ComparedPixels}");
            try
            {
                VisualParityComparator.RequireWithinThreshold(difference, maxMismatchRatio);
            }
            catch (InvalidDataException exception)
            {
                violations.Add(
                    $"{actual.State}: {exception.Message} after the {edgeMaskRadius}px edge/text mask.");
            }
        }

        output.WriteLine("Pixel metrics:");
        foreach (string metric in metrics)
        {
            output.WriteLine(metric);
        }
        output.WriteLine("Representative-region metrics:");
        (string State, string Label, Rectangle Region)[] representativeRegions =
        [
            ("shell.empty", "header", new Rectangle(0, 0, 1200, 44)),
            ("shell.empty", "optional-header", new Rectangle(0, 44, 1200, 40)),
            ("shell.empty", "primary-navigation", new Rectangle(0, 84, 124, 716)),
            ("overlay.start", "start-dialog", new Rectangle(220, 200, 760, 195)),
            ("route.input-elements", "elements-card", new Rectangle(770, 84, 430, 716)),
        ];
        foreach ((string state, string label, Rectangle region) in representativeRegions)
        {
            Capture expected = Assert.Single(
                angular,
                capture => capture.State == state && capture.LogicalSize == new Size(1200, 800) && capture.DpiPercent == 100);
            Capture actual = Assert.Single(
                desktop,
                capture => capture.State == state && capture.LogicalSize == new Size(1200, 800) && capture.DpiPercent == 100);
            VisualParityDifference difference = VisualParityComparator.Compare(
                Path.Combine(Path.GetDirectoryName(ParityTestPaths.ReferenceMetadataPath)!, expected.File),
                Path.Combine(Path.GetDirectoryName(ParityTestPaths.WinFormsMetadataPath)!, actual.File),
                [region],
                maxChannelDelta,
                edgeMaskRadius);
            output.WriteLine(
                $"{label} {region}: meanAbsoluteChannelError={difference.MeanAbsoluteChannelError:F4}, " +
                $"overThresholdPixelRatio={difference.OverThresholdPixelRatio:F6}, " +
                $"edgeMaskedPixels={difference.EdgeMaskedPixels}, comparedPixels={difference.ComparedPixels}");
        }

        Assert.True(
            violations.Count == 0,
            $"Pixel metrics:{Environment.NewLine}{string.Join(Environment.NewLine, metrics)}" +
            $"{Environment.NewLine}Threshold violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static JsonObject LoadMetadata() =>
        LoadMetadata(ParityTestPaths.ReferenceMetadataPath);

    private static JsonObject LoadMetadata(string path) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(path)));

    private static IReadOnlyList<Rectangle> RegionsForState(string state)
    {
        List<Rectangle> regions =
        [
            new Rectangle(0, 0, 1200, 84),
            new Rectangle(0, 84, 124, 716),
        ];
        if (state == "overlay.start")
        {
            regions.Add(new Rectangle(220, 200, 760, 195));
        }
        else if (state == "route.input-elements")
        {
            regions.Add(new Rectangle(770, 84, 430, 716));
        }

        return regions;
    }

    private static Capture ParseCapture(JsonNode? node)
    {
        JsonObject capture = Assert.IsType<JsonObject>(node);
        return new Capture(
            capture["state"]!.GetValue<string>(),
            capture["file"]!.GetValue<string>(),
            new Size(capture["logicalWidth"]!.GetValue<int>(), capture["logicalHeight"]!.GetValue<int>()),
            new Size(capture["imageWidth"]!.GetValue<int>(), capture["imageHeight"]!.GetValue<int>()),
            capture["dpiPercent"]!.GetValue<int>(),
            capture["sha256"]!.GetValue<string>());
    }

    private sealed record Capture(
        string State,
        string File,
        Size LogicalSize,
        Size ImageSize,
        int DpiPercent,
        string Sha256);

}
