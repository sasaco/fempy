using System.Xml.Linq;

namespace PDF_Manager.Core.Tests;

public sealed class ProjectBoundaryTests
{
    private static readonly string[] ForbiddenSourceTerms =
    [
        "System.Windows.Forms",
        "WeifenLuo.WinFormsUI.Docking",
        "OpenTK",
        "OpenGL",
        "GLControl",
        "PdfSharp",
        "PDFsharp",
        "PrintInput",
        "PrintData",
        "Activator.",
        "System.Reflection",
        "Type.GetType",
        "Assembly.Load",
    ];

    [Fact]
    public void CoreProject_TargetsPortableNet8WithoutProjectOrPackageDependencies()
    {
        string projectPath = Path.Combine(FindRepositoryRoot(), "FramePrintPDF", "PDF_Manager.Core", "PDF_Manager.Core.csproj");
        XDocument project = XDocument.Load(projectPath);

        Assert.Equal("net8.0", project.Descendants("TargetFramework").Single().Value);
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
        Assert.Empty(project.Descendants("Reference"));
        Assert.Empty(project.Descendants("UseWindowsForms"));
        Assert.Empty(project.Descendants("UseWPF"));
    }

    [Fact]
    public void CoreSource_DoesNotReferenceUiRenderPdfReflectionOrLegacyPrintContracts()
    {
        string sourceRoot = Path.Combine(FindRepositoryRoot(), "FramePrintPDF", "PDF_Manager.Core");
        string source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(sourceRoot, path)
                    .Split(Path.DirectorySeparatorChar)
                    .Any(part => part is "bin" or "obj"))
                .Select(File.ReadAllText));

        foreach (string forbidden in ForbiddenSourceTerms)
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

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
