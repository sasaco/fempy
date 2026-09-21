using System.Xml.Linq;

namespace PDF_Manager.Rendering.Tests;

public sealed class ProjectBoundaryTests
{
    private static readonly string[] ExpectedPackages =
    [
        "OpenTK.GLControl",
        "OpenTK.Graphics",
        "OpenTK.Mathematics",
        "OpenTK.Windowing.Desktop",
    ];

    [Fact]
    public void RenderingProject_IsWindowsOnlyAndHasNoProductProjectReferences()
    {
        XDocument project = LoadRenderingProject();

        Assert.Equal("net8.0-windows", project.Descendants("TargetFramework").Single().Value);
        Assert.Equal("true", project.Descendants("UseWindowsForms").Single().Value);
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Fact]
    public void RenderingProject_UsesOnlyTheApprovedOpenTkPackageSet()
    {
        XDocument project = LoadRenderingProject();
        string[] packages = project.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedPackages, packages);
    }

    private static XDocument LoadRenderingProject()
    {
        string projectPath = Path.Combine(
            FindRepositoryRoot(),
            "FramePrintPDF",
            "PDF_Manager.Rendering",
            "PDF_Manager.Rendering.csproj");
        return XDocument.Load(projectPath);
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
