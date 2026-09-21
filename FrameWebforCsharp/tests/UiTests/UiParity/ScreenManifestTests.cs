using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;

namespace PDF_Manager.UiTests.UiParity;

public sealed partial class ScreenManifestTests
{
    [Fact]
    public void Manifest_IsCompleteUniqueAndInternallyBidirectional()
    {
        JsonNode manifest = LoadManifest();
        IReadOnlyList<string> errors = ScreenManifestContract.Validate(manifest);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void Manifest_RoutesExactlyMatchAngularRoutingSource()
    {
        JsonObject manifest = Assert.IsType<JsonObject>(LoadManifest());
        JsonArray routes = Assert.IsType<JsonArray>(manifest["routes"]);
        Dictionary<string, string> manifestRoutes = routes.OfType<JsonObject>().ToDictionary(
            route => route["angularRoute"]!.GetValue<string>(),
            route => route["angularComponent"]!.GetValue<string>(),
            StringComparer.Ordinal);

        string routingPath = Path.Combine(ParityTestPaths.RepositoryRoot, "FrameWebforJS", "src", "app", "app-routing.module.ts");
        string source = File.ReadAllText(routingPath);
        Dictionary<string, string> sourceRoutes = AngularRouteRegex().Matches(source)
            .Select(match => (Path: match.Groups["path"].Value, Component: match.Groups["component"].Value))
            .Where(route => route.Path.StartsWith("input-", StringComparison.Ordinal) || route.Path.StartsWith("result-", StringComparison.Ordinal))
            .ToDictionary(route => route.Path, route => route.Component, StringComparer.Ordinal);

        Assert.Equal(sourceRoutes.OrderBy(pair => pair.Key), manifestRoutes.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void Manifest_RoutesAndFieldsExactlyMatchIndependentAngularSourceInventory()
    {
        AngularSourceInventory source = new AngularSourceInventoryExtractor(ParityTestPaths.RepositoryRoot).Extract();
        JsonObject manifest = Assert.IsType<JsonObject>(LoadManifest());
        JsonObject[] routes = Assert.IsType<JsonArray>(manifest["routes"]).OfType<JsonObject>().ToArray();

        Assert.Equal(23, source.Routes.Count);
        foreach (AngularSourceRoute sourceRoute in source.Routes)
        {
            JsonObject manifestRoute = Assert.Single(
                routes,
                route => route["angularRoute"]!.GetValue<string>() == sourceRoute.AngularRoute);
            Assert.Equal(sourceRoute.AngularComponent, manifestRoute["angularComponent"]!.GetValue<string>());
            string[] manifestFields = Assert.IsType<JsonArray>(manifestRoute["fields"])
                .OfType<JsonObject>()
                .Select(field => field["key"]!.GetValue<string>())
                .ToArray();
            Assert.True(
                sourceRoute.FieldKeys.SequenceEqual(manifestFields, StringComparer.Ordinal),
                $"{sourceRoute.AngularRoute} field drift in {sourceRoute.SourceFile}: " +
                $"source=[{string.Join(", ", sourceRoute.FieldKeys)}], " +
                $"manifest=[{string.Join(", ", manifestFields)}]");
        }
    }

    [Fact]
    public void Manifest_ControlsAndNestedPrintStatesHaveIndependentAngularTemplateEvidence()
    {
        AngularSourceInventory source = new AngularSourceInventoryExtractor(ParityTestPaths.RepositoryRoot).Extract();
        JsonObject manifest = Assert.IsType<JsonObject>(LoadManifest());
        JsonObject shell = Assert.IsType<JsonObject>(manifest["shell"]);
        string[] manifestControlKeys =
        [
            .. ControlKeys(shell, "hierarchy"),
            .. ControlKeys(shell, "anonymousMenu"),
            .. ControlKeys(shell, "navigation"),
            .. ControlKeys(shell, "optionalHeaderStates"),
            .. Assert.IsType<JsonArray>(manifest["overlays"])
                .OfType<JsonObject>()
                .SelectMany(overlay => ControlKeys(overlay, "controls")),
            .. Assert.IsType<JsonArray>(manifest["printStates"])
                .OfType<JsonObject>()
                .SelectMany(state => ControlKeys(state, "nestedControls")),
        ];

        string[] missing = manifestControlKeys
            .Where(key => !source.ControlMappingKeys.Contains(key))
            .ToArray();
        string[] extra = source.ControlMappingKeys
            .Where(key => !manifestControlKeys.Contains(key, StringComparer.Ordinal))
            .ToArray();
        Assert.True(
            missing.Length == 0 && extra.Length == 0,
            $"Angular control evidence is not bidirectional. Missing=[{string.Join(", ", missing)}], " +
            $"extra=[{string.Join(", ", extra)}]");
    }

    [Fact]
    public void AngularFieldExtractor_DetectsSourceFieldOmission()
    {
        string path = Path.Combine(
            ParityTestPaths.RepositoryRoot,
            "FrameWebforJS",
            "src",
            "app",
            "components",
            "input",
            "input-elements",
            "input-elements.component.ts");
        string source = File.ReadAllText(path);
        string mutation = source.Replace(", 'Iz'", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(source, mutation);

        string[] fields = AngularSourceInventoryExtractor.ExtractStaticInputFields(mutation);

        Assert.DoesNotContain("Iz", fields);
        Assert.Equal(7, fields.Length);
    }

    [Fact]
    public void AngularControlExtractor_DetectsSourceControlOmission()
    {
        string app = ReadAngular("app.component.html");
        string mutation = app.Replace("routerLink=\"./input-elements\"", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(app, mutation);

        IReadOnlySet<string> controls = AngularSourceInventoryExtractor.ExtractControlEvidenceFromSources(
            mutation,
            ReadAngular("components/menu/menu.component.html"),
            ReadAngular("components/optional-header/optional-header.component.html"),
            ReadAngular("components/start-menu/start-menu.component.html"),
            ReadAngular("components/preset/preset.component.html"),
            ReadAngular("components/print/print.component.html"),
            ReadAngular("components/wait-dialog/wait-dialog.component.html"),
            ReadAngular("components/alert-dialog/alert-dialog.component.html"));

        Assert.DoesNotContain("nav.input.elements", controls);
    }

    [Fact]
    public void Manifest_CSharpRoutesAndTablesExactlyMatchDesktopCatalogs()
    {
        JsonArray routes = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(LoadManifest())["routes"]);
        JsonObject[] routeObjects = routes.OfType<JsonObject>().ToArray();
        string[] manifestRouteIds = routeObjects
            .Select(route => route["csharpRouteId"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(
            Enum.GetNames<ScreenRouteId>().Order(StringComparer.Ordinal),
            manifestRouteIds.Order(StringComparer.Ordinal));

        foreach (InputRouteSurfaceDefinition definition in FrameWebSurfaceCatalog.InputRoutes)
        {
            JsonObject route = Assert.Single(routeObjects,
                candidate => candidate["csharpRouteId"]!.GetValue<string>() == definition.Route.ToString());
            string[] manifestTables = Assert.IsType<JsonArray>(route["tableKeys"])
                .Select(table => table!.GetValue<string>())
                .ToArray();
            Assert.Equal(definition.Tables.Select(table => table.ToString()), manifestTables);
        }

        Assert.Equal(
            AngularScreenManifest.InputRoutes.Select(route => route.Id),
            FrameWebSurfaceCatalog.InputRoutes.Select(route => route.Route));
        Assert.Equal(
            AngularScreenManifest.ResultRoutes.Select(route => route.Id),
            FrameWebSurfaceCatalog.ResultRoutes.Select(route => route.Route));
    }

    [Fact]
    public void Manifest_SourcePathsExistAndSchemaRequiresNoLooseTopLevelProperties()
    {
        JsonObject manifest = Assert.IsType<JsonObject>(LoadManifest());
        JsonArray sources = Assert.IsType<JsonArray>(manifest["sources"]);
        Assert.All(sources, source => Assert.True(
            SourcePatternExists(source!.GetValue<string>()),
            $"Source evidence does not exist: {source}"));

        JsonObject schema = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(ParityTestPaths.SchemaPath)));
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        JsonArray required = Assert.IsType<JsonArray>(schema["required"]);
        Assert.Contains(required, item => item?.GetValue<string>() == "routes");
        Assert.Contains(required, item => item?.GetValue<string>() == "overlays");
        Assert.Contains(required, item => item?.GetValue<string>() == "printStates");
    }

    [Fact]
    public void Validator_RejectsMissingRoute()
    {
        JsonObject mutation = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        Assert.IsType<JsonArray>(mutation["routes"]).RemoveAt(0);
        Assert.Contains(ScreenManifestContract.Validate(mutation), error => error.Contains("23 routes", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsExtraRoute()
    {
        JsonObject mutation = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        JsonArray routes = Assert.IsType<JsonArray>(mutation["routes"]);
        routes.Add(routes[0]!.DeepClone());
        IReadOnlyList<string> errors = ScreenManifestContract.Validate(mutation);
        Assert.Contains(errors, error => error.Contains("23 routes", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsDuplicateFieldMapping()
    {
        JsonObject mutation = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        JsonArray routes = Assert.IsType<JsonArray>(mutation["routes"]);
        JsonArray fields = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(routes[0])["fields"]);
        Assert.IsType<JsonObject>(fields[1])["mappingKey"] = Assert.IsType<JsonObject>(fields[0])["mappingKey"]!.GetValue<string>();
        Assert.Contains(ScreenManifestContract.Validate(mutation), error => error.Contains("Duplicate mappingKey", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsMissingFieldAtFieldGranularity()
    {
        JsonObject mutation = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        JsonObject route = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(mutation["routes"])[0]);
        Assert.IsType<JsonArray>(route["fields"]).RemoveAt(0);
        Assert.Contains(
            ScreenManifestContract.Validate(mutation),
            error => error.Contains("source-derived field inventory", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsExtraFieldAtFieldGranularity()
    {
        JsonObject mutation = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        JsonObject route = Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(mutation["routes"])[0]);
        JsonArray fields = Assert.IsType<JsonArray>(route["fields"]);
        JsonObject extra = Assert.IsType<JsonObject>(fields[0]!.DeepClone());
        extra["mappingKey"] = "route.input.elements.field.mutation-extra";
        extra["key"] = "mutation-extra";
        extra["order"] = fields.Count;
        fields.Add(extra);
        Assert.Contains(
            ScreenManifestContract.Validate(mutation),
            error => error.Contains("source-derived field inventory", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsMissingAndExtraControlsAtControlGranularity()
    {
        JsonObject missing = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        JsonObject missingShell = Assert.IsType<JsonObject>(missing["shell"]);
        Assert.IsType<JsonArray>(missingShell["anonymousMenu"]).RemoveAt(0);
        Assert.Contains(
            ScreenManifestContract.Validate(missing),
            error => error.Contains("controls do not exactly match", StringComparison.Ordinal));

        JsonObject extra = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        JsonObject extraShell = Assert.IsType<JsonObject>(extra["shell"]);
        JsonArray navigation = Assert.IsType<JsonArray>(extraShell["navigation"]);
        JsonObject extraControl = Assert.IsType<JsonObject>(navigation[0]!.DeepClone());
        extraControl["mappingKey"] = "nav.mutation-extra";
        extraControl["order"] = navigation.Count;
        navigation.Add(extraControl);
        Assert.Contains(
            ScreenManifestContract.Validate(extra),
            error => error.Contains("controls do not exactly match", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_RejectsUnapprovedExclusion()
    {
        JsonObject mutation = Assert.IsType<JsonObject>(LoadManifest().DeepClone());
        JsonArray exclusions = Assert.IsType<JsonArray>(mutation["approvedExclusions"]);
        exclusions.Add(new JsonObject
        {
            ["mappingKey"] = "exclusion.unapproved.help",
            ["reason"] = "Mutation",
            ["approvedScope"] = "help",
        });
        Assert.Contains(ScreenManifestContract.Validate(mutation), error => error.Contains("Approved exclusions", StringComparison.Ordinal));
    }

    private static JsonNode LoadManifest() =>
        JsonNode.Parse(File.ReadAllText(ParityTestPaths.ManifestPath))
        ?? throw new InvalidDataException("The parity manifest is empty.");

    private static IEnumerable<string> ControlKeys(JsonObject owner, string property) =>
        Assert.IsType<JsonArray>(owner[property])
            .OfType<JsonObject>()
            .Select(control => control["mappingKey"]!.GetValue<string>());

    private static string ReadAngular(string relativePath) => File.ReadAllText(Path.Combine(
        ParityTestPaths.RepositoryRoot,
        "FrameWebforJS",
        "src",
        "app",
        relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static bool SourcePatternExists(string relativePattern)
    {
        string normalized = relativePattern.Replace('/', Path.DirectorySeparatorChar);
        if (!normalized.Contains('*')) return File.Exists(Path.Combine(ParityTestPaths.RepositoryRoot, normalized));

        int wildcard = normalized.IndexOf('*');
        string prefix = normalized[..wildcard].TrimEnd(Path.DirectorySeparatorChar);
        string directory = Path.Combine(ParityTestPaths.RepositoryRoot, prefix);
        return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.ts", SearchOption.AllDirectories).Any();
    }

    [GeneratedRegex("\\{\\s*path:\\s*['\"](?<path>[^'\"]+)['\"]\\s*,\\s*component:\\s*(?<component>[A-Za-z0-9_]+)", RegexOptions.CultureInvariant)]
    private static partial Regex AngularRouteRegex();
}
