using System.Xml.Linq;

namespace PDF_Manager.Tests;

public sealed class ProjectBoundaryTests
{
    private static readonly string[] ForbiddenPrintingSourceTerms =
    [
        "Dictionary<string, object>",
        "Function1",
        "Function2",
        "Newtonsoft.Json",
        "PdfSharp",
        "PrintData",
        "PrintInput",
    ];

    private static readonly string[] ExpectedProjectReferences =
    [
        "PDF_Manager.Core/PDF_Manager.Core.csproj",
        "PDF_Manager.Printing/PDF_Manager.Printing.csproj",
        "PDF_Manager.Rendering/PDF_Manager.Rendering.csproj",
    ];

    private static readonly string[] ExpectedShellPackages =
    [
        "DockPanelSuite",
        "DockPanelSuite.ThemeVS2015",
    ];

    private static readonly string[] LegacyCompileExclusions =
    [
        "IPrintable.cs",
        "PrintData.cs",
        "PrintInput.cs",
        "PrintableBase*.cs",
        "Printing/**/*.cs",
    ];

    [Fact]
    public void CompositionRoot_IsNet8WindowsWinFormsExecutable()
    {
        XDocument project = LoadCompositionRootProject();

        Assert.Equal("WinExe", SingleProperty(project, "OutputType"));
        Assert.Equal("net8.0-windows", SingleProperty(project, "TargetFramework"));
        Assert.Equal("true", SingleProperty(project, "UseWindowsForms"));
    }

    [Fact]
    public void CompositionRoot_ReferencesOnlyCoreRenderingAndPrintingProjects()
    {
        XDocument project = LoadCompositionRootProject();
        string[] references = project.Descendants("ProjectReference")
            .Select(element => NormalizePath(element.Attribute("Include")?.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedProjectReferences, references);
    }

    [Fact]
    public void CompositionRoot_KeepsOpenGlAndPdfPackagesBehindLayerBoundaries()
    {
        XDocument project = LoadCompositionRootProject();
        string[] packages = project.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedShellPackages, packages);
    }

    [Fact]
    public void CompositionRoot_ExcludesLegacyPrintSourcesAndRestrictedFontsFromBuildItems()
    {
        XDocument project = LoadCompositionRootProject();
        string[] compileExclusions = SplitItems(project.Descendants("Compile"), "Remove");

        Assert.Equal(LegacyCompileExclusions, compileExclusions);

        foreach (string itemType in new[] { "Content", "EmbeddedResource", "None" })
        {
            string[] exclusions = SplitItems(project.Descendants(itemType), "Remove");
            Assert.Contains("fonts/**", exclusions);
            Assert.Contains("Printing/**", exclusions);
            Assert.Contains("PrintInput.cs", exclusions);
            Assert.Contains("PrintData.cs", exclusions);
        }
    }

    [Fact]
    public void PrintingProject_TargetsPortableNet8WithoutExternalDependencies()
    {
        XDocument project = LoadProject("PDF_Manager.Printing", "PDF_Manager.Printing.csproj");

        Assert.Equal("net8.0", SingleProperty(project, "TargetFramework"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
        Assert.Empty(project.Descendants("Reference"));
        Assert.Empty(project.Descendants("UseWindowsForms"));
    }

    [Fact]
    public void PrintingSource_UsesNoLegacyUntypedOrPdfContracts()
    {
        string sourceRoot = Path.Combine(FindRepositoryRoot(), "FramePrintPDF", "PDF_Manager.Printing");
        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(sourceRoot, path)
                    .Split(Path.DirectorySeparatorChar)
                    .Any(part => part is "bin" or "obj"))
                .Select(File.ReadAllText));

        foreach (string forbidden in ForbiddenPrintingSourceTerms)
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    private static XDocument LoadCompositionRootProject()
        => LoadProject("PDF_Manager", "PDF_Manager.csproj");

    private static XDocument LoadProject(string directoryName, string projectFileName) =>
        XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "FramePrintPDF",
            directoryName,
            projectFileName));

    private static string SingleProperty(XDocument project, string propertyName) =>
        project.Descendants(propertyName).Single().Value;

    private static string[] SplitItems(IEnumerable<XElement> elements, string attributeName) =>
        elements
            .Select(element => element.Attribute(attributeName)?.Value)
            .OfType<string>()
            .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(NormalizePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string NormalizePath(string? value) =>
        value?.Replace('\\', '/').Replace("../", string.Empty, StringComparison.Ordinal) ?? string.Empty;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
