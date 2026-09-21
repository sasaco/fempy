namespace PDF_Manager.UiTests.UiParity;

internal static class ParityTestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string UiParityRoot => Path.Combine(
        RepositoryRoot,
        "FramePrintPDF",
        "PDF_Manager.UiTests",
        "UiParity");

    public static string ManifestPath => Path.Combine(UiParityRoot, "framewebforjs-screen-manifest.v1.json");

    public static string SchemaPath => Path.Combine(UiParityRoot, "framewebforjs-screen-manifest.v1.schema.json");

    public static string ReferenceMetadataPath => Path.Combine(
        UiParityRoot,
        "References",
        "angular-v1",
        "reference-captures.v1.json");

    public static string WinFormsMetadataPath => Path.Combine(
        UiParityRoot,
        "References",
        "winforms-v1",
        "winforms-captures.v1.json");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? candidate = new(AppContext.BaseDirectory);
        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "FrameWeb.sln")) &&
                Directory.Exists(Path.Combine(candidate.FullName, "FrameWebforJS")))
            {
                return candidate.FullName;
            }

            candidate = candidate.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the FrameWeb3 repository root.");
    }
}
