using System.Text.RegularExpressions;

namespace PDF_Manager.UiTests.UiParity;

internal sealed partial class AngularSourceInventoryExtractor
{
    private readonly string angularRoot;

    public AngularSourceInventoryExtractor(string repositoryRoot)
    {
        angularRoot = Path.Combine(repositoryRoot, "FrameWebforJS", "src", "app");
    }

    public AngularSourceInventory Extract()
    {
        string routingSource = Read("app-routing.module.ts");
        Dictionary<string, string> componentFiles = Directory
            .EnumerateFiles(angularRoot, "*.component.ts", SearchOption.AllDirectories)
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .SelectMany(item => ExportedClassRegex().Matches(item.Source)
                .Select(match => (Class: match.Groups["class"].Value, item.Path)))
            .ToDictionary(item => item.Class, item => item.Path, StringComparer.Ordinal);

        List<AngularSourceRoute> routes = [];
        foreach (Match match in RouteRegex().Matches(routingSource))
        {
            string route = match.Groups["path"].Value;
            if (!route.StartsWith("input-", StringComparison.Ordinal) &&
                !route.StartsWith("result-", StringComparison.Ordinal))
            {
                continue;
            }

            string component = match.Groups["component"].Value;
            if (!componentFiles.TryGetValue(component, out string? componentPath))
            {
                throw new InvalidDataException($"Angular route '{route}' references unknown component '{component}'.");
            }

            string[] fields = route.StartsWith("input-", StringComparison.Ordinal)
                ? ExtractInputFields(route, componentPath)
                : ExtractResultFields(componentPath);
            if (fields.Length == 0)
            {
                throw new InvalidDataException($"Angular route '{route}' yielded no source-derived fields.");
            }

            routes.Add(new AngularSourceRoute(route, component, fields, Relative(componentPath)));
        }

        return new AngularSourceInventory(routes, ExtractControlEvidence());
    }

    internal static string[] ExtractStaticInputFields(string source, string siblingSource = "")
    {
        source = StripTypeScriptComments(source);
        siblingSource = StripTypeScriptComments(siblingSource);
        string[] threeDimensional = ExtractStringArray(source, "columnKeys3D");
        if (threeDimensional.Length > 0) return threeDimensional;

        string[] keys = ExtractStringArray(source, "columnKeys");
        string[] literalDataIndices = DataIndexLiteralRegex().Matches(source)
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (literalDataIndices.Length >= keys.Length && literalDataIndices.Length > 0)
        {
            keys = literalDataIndices;
        }

        Match generated = GeneratedColumnRegex().Match(source);
        if (generated.Success)
        {
            string countName = generated.Groups["count"].Value;
            Match count = Regex.Match(
                siblingSource,
                $@"\b{Regex.Escape(countName)}\s*=\s*(?<value>\d+)\b",
                RegexOptions.CultureInvariant);
            if (!count.Success)
            {
                throw new InvalidDataException($"Could not resolve generated Angular field count '{countName}'.");
            }

            string prefix = generated.Groups["prefix"].Value;
            keys = [.. keys, .. Enumerable.Range(1, int.Parse(count.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture)).Select(index => $"{prefix}{index}")];
        }

        return keys;
    }

    internal static IReadOnlySet<string> ExtractControlEvidenceFromSources(
        string appHtml,
        string menuHtml,
        string optionalHeaderHtml,
        string startHtml,
        string presetHtml,
        string printHtml,
        string waitHtml,
        string alertHtml)
    {
        Dictionary<string, (string Source, string Pattern)> evidence = new(StringComparer.Ordinal)
        {
            ["shell.header.menu"] = (appHtml, @"<app-menu\b"),
            ["shell.header.optional"] = (appHtml, @"<app-optional-header\b"),
            ["shell.navigation.primary"] = (appHtml, @"<nav\s+class=\""nav-left-body-container\"""),
            ["shell.workspace.viewport"] = (appHtml, @"<app-three\b"),
            ["shell.route.panel"] = (appHtml, @"id=\""contents-dialog-id\"""),
            ["shell.overlay.host"] = (appHtml, @"<router-outlet\s+name=\""startOutlet\"""),

            ["menu.file.root"] = (menuHtml, @"ngbDropdownToggle"),
            ["menu.file.new"] = (menuHtml, @"\(click\)=\""renew\(\)\"""),
            ["menu.file.open"] = (menuHtml, @"type=\""file\"""),
            ["menu.file.save"] = (menuHtml, @"\(click\)=\""save\(\)\"""),
            ["menu.file.preset"] = (menuHtml, @"presetOutlet"),
            ["menu.file.overwrite"] = (menuHtml, @"\(click\)=\""overWrite\(\)\"""),
            ["menu.file.output"] = (menuHtml, @"\(click\)=\""pickup\(\)\"""),
            ["menu.file.newwindow"] = (menuHtml, @"\(click\)=\""newWindow\(\)\"""),
            ["menu.print"] = (menuHtml, @"printOutlet"),
            ["menu.language"] = (menuHtml, @"language\.trans\(index\.key\)"),
            ["menu.help"] = (menuHtml, @"\(click\)=\""goToLink\(\)\"""),
            ["menu.contact"] = (menuHtml, @"\(click\)=\""handelClickChat\(\)\"""),
            ["menu.auth.login"] = (menuHtml, @"\(click\)=\""login\(\)\"""),

            ["nav.calculate"] = (appHtml, @"\(click\)=\""calcrate\(\)\"""),
            ["nav.input.elements"] = (appHtml, @"routerLink=\""\./input-elements\"""),
            ["nav.input.nodes"] = (appHtml, @"routerLink=\""\./input-nodes\"""),
            ["nav.input.supports"] = (appHtml, @"routerLink=\""\./input-fix_nodes\"""),
            ["nav.input.members"] = (appHtml, @"routerLink=\""\./input-members\"""),
            ["nav.input.panel"] = (appHtml, @"routerLink=\""\./input-panel\"""),
            ["nav.input.joints"] = (appHtml, @"routerLink=\""\./input-joints\"""),
            ["nav.input.notice"] = (appHtml, @"routerLink=\""\./input-notice_points\"""),
            ["nav.input.springs"] = (appHtml, @"routerLink=\""\./input-fix_members\"""),
            ["nav.input.loads"] = (appHtml, @"routerLink=\""\./input-load-name\"""),
            ["nav.input.define"] = (appHtml, @"routerLink=\""\./input-define\"""),
            ["nav.result.displacement"] = (appHtml, @"\[routerLink\]=\""getDisgLink\(\)\"""),
            ["nav.result.reaction"] = (appHtml, @"\[routerLink\]=\""getReacLink\(\)\"""),
            ["nav.result.sectionforce"] = (appHtml, @"\[routerLink\]=\""getFsecLink\(\)\"""),

            ["header.dimension"] = (optionalHeaderHtml, @"handleShow\(2\).*handleShow\(3\)"),
            ["header.load"] = (optionalHeaderHtml, @"previousLoadPage\(\).*nextLoadPage\(\)"),
            ["header.define"] = (optionalHeaderHtml, @"handleDefinePage\('/input-define'\).*handleDefinePage\('/input-combine'\).*handleDefinePage\('/input-pickup'\)"),
            ["header.result"] = (optionalHeaderHtml, @"handleResultPage\(1\).*handleResultPage\(2\).*handleResultPage\(3\)"),
            ["header.pager"] = (optionalHeaderHtml, @"<app-pager\b"),
            ["header.direction"] = (optionalHeaderHtml, @"<app-pager-direction\b"),
            ["header.member"] = (optionalHeaderHtml, @"previousMemberPage\(\).*nextMemberPage\(\)"),
            ["header.control"] = (optionalHeaderHtml, @"onToggleControl\(\)"),

            ["overlay.start.close"] = (startHtml, @"onPageBack\(\)"),
            ["overlay.start.new"] = (startHtml, @"clickStartNew\(\)"),
            ["overlay.start.open"] = (startHtml, @"clickOpenFile\(\$event\)"),
            ["overlay.start.preset"] = (startHtml, @"clickSelectPreset\(\)"),
            ["overlay.preset.close"] = (presetHtml, @"onPageBack\(\)"),
            ["overlay.preset.choices"] = (presetHtml, @"presetService\.selectRadio\(item\)"),
            ["overlay.preset.cancel"] = (presetHtml, @"class=\""button cancel\"""),
            ["overlay.preset.open"] = (presetHtml, @"\(click\)=\""openFile\(\)\"""),
            ["overlay.print.close"] = (printHtml, @"onPageBack\(\)"),
            ["overlay.print.selection"] = (printHtml, @"printService\.selectCheckbox\("),
            ["overlay.print.preview"] = (printHtml, @"<app-print-custom\b"),
            ["overlay.print.cancel"] = (printHtml, @"class=\""button cancel\"""),
            ["overlay.print.pdf"] = (printHtml, @"onPrintPDFNew\(\)"),
            ["overlay.wait.progress"] = (waitHtml, @"class=\""spinner\"""),
            ["overlay.confirm.message"] = (alertHtml, @"\{\{\s*message\s*\}\}"),
            ["overlay.confirm.cancel"] = (alertHtml, @"modal\.close\('no'\)"),
            ["overlay.confirm.ok"] = (alertHtml, @"modal\.close\('yes'\)"),
            ["overlay.alert.message"] = (alertHtml, @"\{\{\s*message\s*\}\}"),
            ["overlay.alert.ok"] = (alertHtml, @"modal\.close\('ok'\)"),

            ["print.input.all"] = (printHtml, @"printService\.checkAll\(\)"),
            ["print.result.displacement.minmax"] = (printHtml, @"id=\""print1\"""),
            ["print.result.reaction.minmax"] = (printHtml, @"id=\""print4\"""),
            ["print.result.section_force.minmax"] = (printHtml, @"id=\""print7\"""),
            ["print.viewport.load.target"] = (printHtml, @"id=\""printCase5\"""),
            ["print.viewport.result.components"] = (printHtml, @"id=\""printCase1\"""),
            ["print.preview.pages.pager"] = (printHtml, @"class=\""resultArea\"""),
        };

        return evidence
            .Where(pair => Regex.IsMatch(
                StripHtmlComments(pair.Value.Source),
                pair.Value.Pattern,
                RegexOptions.CultureInvariant | RegexOptions.Singleline))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private string[] ExtractInputFields(string route, string componentPath)
    {
        string source = File.ReadAllText(componentPath);
        if (route == "input-define") return [ExtractDefineSymbol(source)];
        if (route == "input-combine") return [ExtractCombineSymbol(source), .. ExtractLiteralDataIndices(source)];
        if (route == "input-pickup") return [ExtractPickupSymbol(source), .. ExtractLiteralDataIndices(source)];

        string siblingSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.GetDirectoryName(componentPath)!, "*.service.ts")
                .Select(File.ReadAllText));
        string[] fields = ExtractStaticInputFields(source, siblingSource);
        if (route == "input-load-name")
        {
            string loadSource = Read(Path.Combine("components", "input", "input-load", "input-load.component.html"));
            if (Regex.IsMatch(loadSource, @"\[\(ngModel\)\]=\""LL_pitch\""", RegexOptions.CultureInvariant))
            {
                fields = [.. fields, "LL_pitch"];
            }
        }

        return fields;
    }

    private static string[] ExtractResultFields(string componentPath)
    {
        return Directory.EnumerateFiles(Path.GetDirectoryName(componentPath)!, "*.ts")
            .Select(File.ReadAllText)
            .SelectMany(source => ExtractNamedArrayBodies(source, "column3Ds"))
            .Select(body => IdLiteralRegex().Matches(body).Select(match => match.Groups["key"].Value).ToArray())
            .Where(keys => keys.Length > 0)
            .OrderByDescending(keys => keys.Length)
            .FirstOrDefault() ?? [];
    }

    private IReadOnlySet<string> ExtractControlEvidence() => ExtractControlEvidenceFromSources(
        Read("app.component.html"),
        Read(Path.Combine("components", "menu", "menu.component.html")),
        Read(Path.Combine("components", "optional-header", "optional-header.component.html")),
        Read(Path.Combine("components", "start-menu", "start-menu.component.html")),
        Read(Path.Combine("components", "preset", "preset.component.html")),
        Read(Path.Combine("components", "print", "print.component.html")),
        Read(Path.Combine("components", "wait-dialog", "wait-dialog.component.html")),
        Read(Path.Combine("components", "alert-dialog", "alert-dialog.component.html")));

    private static string ExtractDefineSymbol(string source)
    {
        RequireSource(source, @"getLoadCaseCount\(\)\s*\*\s*2\s*\+\s*1", "DEFINE load-case formula");
        RequireSource(source, @"COLUMNS_COUNT\s*<=\s*10", "DEFINE minimum column count");
        return "C1..C(2*loadCases+1,min10)";
    }

    private static string ExtractCombineSymbol(string source)
    {
        RequireSource(source, @"getDefineCaseCount\(\)", "COMBINE define-case source");
        RequireSource(source, @"getLoadCaseCount\(\)", "COMBINE load-case fallback");
        RequireSource(source, @"COLUMNS_COUNT\s*<=\s*5", "COMBINE minimum column count");
        return "C1..C(max(defineCases,loadCases,5))";
    }

    private static string ExtractPickupSymbol(string source)
    {
        RequireSource(source, @"getCombineCaseCount\(\)", "PICKUP combine-case source");
        RequireSource(source, @"getLoadCaseCount\(\)", "PICKUP load-case fallback");
        RequireSource(source, @"COLUMNS_COUNT\s*<=\s*5", "PICKUP minimum column count");
        return "C1..C(max(combineCases,loadCases,5))";
    }

    private static string[] ExtractLiteralDataIndices(string source) =>
        DataIndexLiteralRegex().Matches(StripTypeScriptComments(source))
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string[] ExtractStringArray(string source, string property)
    {
        return ExtractNamedArrayBodies(source, property)
            .Select(body => StringLiteralRegex().Matches(body).Select(match => match.Groups["value"].Value).ToArray())
            .FirstOrDefault() ?? [];
    }

    private static IEnumerable<string> ExtractNamedArrayBodies(string source, string property)
    {
        foreach (Match match in Regex.Matches(
            source,
            $@"\b{Regex.Escape(property)}\b[^=]*=\s*\[",
            RegexOptions.CultureInvariant))
        {
            int start = match.Index + match.Length;
            int depth = 1;
            bool inString = false;
            char delimiter = '\0';
            for (int index = start; index < source.Length; index++)
            {
                char current = source[index];
                if (inString)
                {
                    if (current == '\\') index++;
                    else if (current == delimiter) inString = false;
                    continue;
                }

                if (current is '\'' or '"' or '`')
                {
                    inString = true;
                    delimiter = current;
                }
                else if (current == '[') depth++;
                else if (current == ']' && --depth == 0)
                {
                    yield return source[start..index];
                    break;
                }
            }
        }
    }

    private static void RequireSource(string source, string pattern, string label)
    {
        if (!Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException($"Angular source no longer contains {label}.");
        }
    }

    private string Read(string relativePath) => File.ReadAllText(Path.Combine(angularRoot, relativePath));

    private string Relative(string path) => Path.GetRelativePath(
        Path.Combine(angularRoot, "..", "..", ".."),
        path).Replace('\\', '/');

    private static string StripHtmlComments(string source) => Regex.Replace(
        source,
        "<!--.*?-->",
        string.Empty,
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static string StripTypeScriptComments(string source)
    {
        string withoutBlocks = Regex.Replace(
            source,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return Regex.Replace(
            withoutBlocks,
            @"//[^\r\n]*",
            string.Empty,
            RegexOptions.CultureInvariant);
    }

    [GeneratedRegex("""\{\s*path:\s*['"](?<path>[^'"]+)['"]\s*,\s*component:\s*(?<component>[A-Za-z0-9_]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex RouteRegex();

    [GeneratedRegex(@"\bexport\s+class\s+(?<class>[A-Za-z0-9_]+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ExportedClassRegex();

    [GeneratedRegex("""dataIndx\s*:\s*['"](?<key>[^'"]+)['"](?!\s*\+)""", RegexOptions.CultureInvariant)]
    private static partial Regex DataIndexLiteralRegex();

    [GeneratedRegex("""\bid\s*:\s*['"](?<key>[^'"]+)['"]""", RegexOptions.CultureInvariant)]
    private static partial Regex IdLiteralRegex();

    [GeneratedRegex("""['"](?<value>[^'"]+)['"]""", RegexOptions.CultureInvariant)]
    private static partial Regex StringLiteralRegex();

    [GeneratedRegex("""columnKeys\.push\(\s*['"](?<prefix>[^'"]+)['"]\s*\+\s*i(?:\.toString\(\))?\s*\).*?this\.data\.(?<count>[A-Z_]+)""", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex GeneratedColumnRegex();
}

internal sealed record AngularSourceInventory(
    IReadOnlyList<AngularSourceRoute> Routes,
    IReadOnlySet<string> ControlMappingKeys);

internal sealed record AngularSourceRoute(
    string AngularRoute,
    string AngularComponent,
    IReadOnlyList<string> FieldKeys,
    string SourceFile);
