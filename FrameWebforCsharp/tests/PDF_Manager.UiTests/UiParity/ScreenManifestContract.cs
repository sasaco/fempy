using System.Text.Json.Nodes;

namespace PDF_Manager.UiTests.UiParity;

internal static class ScreenManifestContract
{
    private static readonly HashSet<string> ExpectedExclusions = new(StringComparer.Ordinal)
    {
        "login",
        "authenticated-mypage",
        "authenticated-logout",
    };

    private static readonly HashSet<string> ExpectedOverlayKinds = new(StringComparer.Ordinal)
    {
        "start",
        "preset",
        "print",
        "wait",
        "confirm",
        "alert",
    };

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedFieldKeysByRoute =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["route.input.elements"] = ["E", "G", "Xp", "A", "J", "Iy", "Iz", "n"],
            ["route.input.nodes"] = ["x", "y", "z"],
            ["route.input.supports"] = ["n", "tx", "ty", "tz", "rx", "ry", "rz"],
            ["route.input.members"] = ["ni", "nj", "L", "e", "cg", "n"],
            ["route.input.rigid_zone"] = ["m", "L", "e", "n", "Ilength", "Jlength", "e1"],
            ["route.input.panel"] = ["e", "point-1", "point-2", "point-3", "point-4"],
            ["route.input.joints"] = ["m", "xi", "yi", "zi", "xj", "yj", "zj"],
            ["route.input.notice_points"] = ["m", "len", .. Enumerable.Range(1, 20).Select(index => $"L{index}")],
            ["route.input.member_springs"] = ["m", "tx", "ty", "tz", "tr"],
            ["route.input.load_name"] = ["symbol", "name", "fix_node", "element", "fix_member", "joint", "LL_pitch"],
            ["route.input.loads"] = ["m1", "m2", "direction", "mark", "L1", "L2", "P1", "P2", "n", "tx", "ty", "tz", "rx", "ry", "rz"],
            ["route.input.define"] = ["C1..C(2*loadCases+1,min10)"],
            ["route.input.combine"] = ["C1..C(max(defineCases,loadCases,5))", "name"],
            ["route.input.pickup"] = ["C1..C(max(combineCases,loadCases,5))", "name"],
            ["route.result.displacement.basic"] = ["id", "dx", "dy", "dz", "rx", "ry", "rz"],
            ["route.result.reaction.basic"] = ["id", "tx", "ty", "tz", "mx", "my", "mz"],
            ["route.result.section_force.basic"] = ["m", "n", "l", "fx", "fy", "fz", "mx", "my", "mz"],
            ["route.result.displacement.combine"] = ["id", "dx", "dy", "dz", "rx", "ry", "rz", "case"],
            ["route.result.reaction.combine"] = ["id", "tx", "ty", "tz", "mx", "my", "mz", "case"],
            ["route.result.section_force.combine"] = ["m", "n", "l", "fx", "fy", "fz", "mx", "my", "mz", "case"],
            ["route.result.displacement.pickup"] = ["id", "dx", "dy", "dz", "rx", "ry", "rz", "case"],
            ["route.result.reaction.pickup"] = ["id", "tx", "ty", "tz", "mx", "my", "mz", "case"],
            ["route.result.section_force.pickup"] = ["m", "n", "l", "fx", "fy", "fz", "mx", "my", "mz", "case"],
        };

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedShellControlKeys =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["hierarchy"] = ["shell.header.menu", "shell.header.optional", "shell.navigation.primary", "shell.workspace.viewport", "shell.route.panel", "shell.overlay.host"],
            ["anonymousMenu"] = ["menu.file.root", "menu.file.new", "menu.file.open", "menu.file.save", "menu.file.preset", "menu.file.overwrite", "menu.file.output", "menu.file.newwindow", "menu.print", "menu.language", "menu.help", "menu.contact", "menu.auth.login"],
            ["navigation"] = ["nav.calculate", "nav.input.elements", "nav.input.nodes", "nav.input.supports", "nav.input.members", "nav.input.panel", "nav.input.joints", "nav.input.notice", "nav.input.springs", "nav.input.loads", "nav.input.define", "nav.result.displacement", "nav.result.reaction", "nav.result.sectionforce"],
            ["optionalHeaderStates"] = ["header.dimension", "header.load", "header.define", "header.result", "header.pager", "header.direction", "header.member", "header.control"],
        };

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedOverlayControlKeys =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["start"] = ["overlay.start.close", "overlay.start.new", "overlay.start.open", "overlay.start.preset"],
            ["preset"] = ["overlay.preset.close", "overlay.preset.choices", "overlay.preset.cancel", "overlay.preset.open"],
            ["print"] = ["overlay.print.close", "overlay.print.selection", "overlay.print.preview", "overlay.print.cancel", "overlay.print.pdf"],
            ["wait"] = ["overlay.wait.progress"],
            ["confirm"] = ["overlay.confirm.message", "overlay.confirm.cancel", "overlay.confirm.ok"],
            ["alert"] = ["overlay.alert.message", "overlay.alert.ok"],
        };

    private static readonly string[] ExpectedPrintStates =
    [
        "print.input",
        "print.result.displacement",
        "print.result.reaction",
        "print.result.section_force",
        "print.viewport.load",
        "print.viewport.result",
        "print.preview.pages",
    ];

    private static readonly IReadOnlyDictionary<string, string> ExpectedPrintNestedControlKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["print.input"] = "print.input.all",
            ["print.result.displacement"] = "print.result.displacement.minmax",
            ["print.result.reaction"] = "print.result.reaction.minmax",
            ["print.result.section_force"] = "print.result.section_force.minmax",
            ["print.viewport.load"] = "print.viewport.load.target",
            ["print.viewport.result"] = "print.viewport.result.components",
            ["print.preview.pages"] = "print.preview.pages.pager",
        };

    public static IReadOnlyList<string> Validate(JsonNode? root)
    {
        List<string> errors = [];
        if (root is not JsonObject document)
        {
            return ["The manifest root must be a JSON object."];
        }

        Require(document["manifestVersion"]?.GetValue<int>() == 1, "manifestVersion must equal 1.", errors);
        JsonArray routes = RequireArray(document, "routes", errors);
        JsonArray exclusions = RequireArray(document, "approvedExclusions", errors);
        JsonArray overlays = RequireArray(document, "overlays", errors);
        JsonArray printStates = RequireArray(document, "printStates", errors);
        JsonObject shell = RequireObject(document, "shell", errors);

        ValidateMappingKeys(document, errors);
        ValidateRoutes(routes, errors);
        ValidateExclusions(exclusions, errors);
        ValidateOverlays(overlays, errors);
        ValidatePrintStates(printStates, errors);
        foreach ((string property, string[] expected) in ExpectedShellControlKeys)
        {
            ValidateOrderedControls(shell, property, expected, errors);
        }
        return errors;
    }

    private static void ValidateMappingKeys(JsonObject document, List<string> errors)
    {
        List<string> keys = [];
        CollectMappingKeys(document, keys, errors);
        foreach (IGrouping<string, string> duplicate in keys.GroupBy(static key => key, StringComparer.Ordinal).Where(static group => group.Count() > 1))
        {
            errors.Add($"Duplicate mappingKey: {duplicate.Key}");
        }
    }

    private static void CollectMappingKeys(JsonNode node, List<string> keys, List<string> errors)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("mappingKey", out JsonNode? mappingNode))
            {
                string? mapping = mappingNode?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(mapping)) errors.Add("mappingKey cannot be blank.");
                else keys.Add(mapping);
            }

            foreach ((_, JsonNode? child) in obj)
            {
                if (child is not null) CollectMappingKeys(child, keys, errors);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null) CollectMappingKeys(child, keys, errors);
            }
        }
    }

    private static void ValidateRoutes(JsonArray routes, List<string> errors)
    {
        JsonObject[] routeObjects = routes.OfType<JsonObject>().ToArray();
        Require(routeObjects.Length == 23, $"Expected exactly 23 routes, found {routeObjects.Length}.", errors);
        Require(routeObjects.Count(RouteFamily("input")) == 14, "Expected exactly 14 input routes.", errors);
        Require(routeObjects.Count(RouteFamily("result")) == 9, "Expected exactly 9 result routes.", errors);
        RequireUnique(routeObjects, "angularRoute", errors);
        RequireUnique(routeObjects, "csharpRouteId", errors);
        RequireUnique(routeObjects, "mappingKey", errors);
        HashSet<string> actualRouteKeys = routeObjects
            .Select(route => route["mappingKey"]?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        Require(actualRouteKeys.SetEquals(ExpectedFieldKeysByRoute.Keys), "Route mapping keys do not match the source-derived inventory.", errors);

        foreach (JsonObject route in routeObjects)
        {
            string routeKey = route["mappingKey"]?.GetValue<string>() ?? "<unknown-route>";
            Require(!string.IsNullOrWhiteSpace(route["angularComponent"]?.GetValue<string>()), $"{routeKey} lacks angularComponent.", errors);
            Require(!string.IsNullOrWhiteSpace(route["navigationKey"]?.GetValue<string>()), $"{routeKey} lacks navigationKey.", errors);
            Require(route["defaultPanelVisible"]?.GetValue<bool>() == false, $"{routeKey} must default to a hidden route panel.", errors);

            JsonArray fields = RequireArray(route, "fields", errors);
            Require(fields.Count > 0, $"{routeKey} must inventory at least one field.", errors);
            ValidateFields(routeKey, fields, errors);
            if (ExpectedFieldKeysByRoute.TryGetValue(routeKey, out string[]? expectedFields))
            {
                string[] actualFields = fields.OfType<JsonObject>()
                    .Select(field => field["key"]?.GetValue<string>() ?? string.Empty)
                    .ToArray();
                Require(actualFields.SequenceEqual(expectedFields, StringComparer.Ordinal),
                    $"{routeKey} fields do not exactly match the source-derived field inventory.", errors);
            }
            Require(RequireArray(route, "tableKeys", errors).Count > 0, $"{routeKey} must map at least one C# table/result surface.", errors);
            Require(RequireArray(route, "actions", errors).Count > 0, $"{routeKey} must inventory actions.", errors);
            Require(RequireArray(route, "transitions", errors).Count > 0, $"{routeKey} must inventory transitions.", errors);
        }
    }

    private static Func<JsonObject, bool> RouteFamily(string family) => route =>
        string.Equals(route["family"]?.GetValue<string>(), family, StringComparison.Ordinal);

    private static void ValidateFields(string routeKey, JsonArray fields, List<string> errors)
    {
        JsonObject[] fieldObjects = fields.OfType<JsonObject>().ToArray();
        Require(fieldObjects.Length == fields.Count, $"{routeKey} contains a non-object field.", errors);
        RequireUnique(fieldObjects, "key", errors, routeKey);
        RequireUnique(fieldObjects, "mappingKey", errors, routeKey);
        int[] orders = fieldObjects.Select(field => field["order"]?.GetValue<int>() ?? -1).Order().ToArray();
        Require(orders.SequenceEqual(Enumerable.Range(0, orders.Length)), $"{routeKey} field order must be contiguous from zero.", errors);

        foreach (JsonObject field in fieldObjects)
        {
            string fieldKey = field["mappingKey"]?.GetValue<string>() ?? $"{routeKey}.<unknown-field>";
            foreach (string property in new[] { "group", "kind", "visibleWhen", "csharpTableKey", "csharpColumnId" })
            {
                Require(!string.IsNullOrWhiteSpace(field[property]?.GetValue<string>()), $"{fieldKey} lacks {property}.", errors);
            }

            Require(field["readOnly"] is JsonValue, $"{fieldKey} lacks readOnly.", errors);
            Require(field.ContainsKey("defaultValue"), $"{fieldKey} lacks defaultValue.", errors);
        }
    }

    private static void ValidateExclusions(JsonArray exclusions, List<string> errors)
    {
        HashSet<string> actual = exclusions
            .OfType<JsonObject>()
            .Select(exclusion => exclusion["approvedScope"]?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(ExpectedExclusions),
            $"Approved exclusions must be exactly: {string.Join(", ", ExpectedExclusions.Order())}.", errors);
    }

    private static void ValidateOverlays(JsonArray overlays, List<string> errors)
    {
        JsonObject[] overlayObjects = overlays.OfType<JsonObject>().ToArray();
        HashSet<string> actual = overlayObjects
            .Select(overlay => overlay["kind"]?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(ExpectedOverlayKinds),
            $"Overlay kinds must be exactly: {string.Join(", ", ExpectedOverlayKinds.Order())}.", errors);
        foreach (JsonObject overlay in overlayObjects)
        {
            string kind = overlay["kind"]?.GetValue<string>() ?? string.Empty;
            if (ExpectedOverlayControlKeys.TryGetValue(kind, out string[]? expected))
            {
                ValidateOrderedControls(overlay, "controls", expected, errors);
            }
        }
    }

    private static void ValidatePrintStates(JsonArray printStates, List<string> errors)
    {
        JsonObject[] states = printStates.OfType<JsonObject>().ToArray();
        string[] actual = states.Select(state => state["mappingKey"]?.GetValue<string>() ?? string.Empty).ToArray();
        Require(actual.SequenceEqual(ExpectedPrintStates, StringComparer.Ordinal),
            "printStates do not exactly match the nested print-state inventory.", errors);
        RequireUnique(states, "mappingKey", errors, "printStates");
        int[] orders = states.Select(state => state["order"]?.GetValue<int>() ?? -1).Order().ToArray();
        Require(orders.SequenceEqual(Enumerable.Range(0, states.Length)), "printStates order must be contiguous from zero.", errors);
        foreach (JsonObject state in states)
        {
            string stateKey = state["mappingKey"]?.GetValue<string>() ?? string.Empty;
            foreach (string required in new[] { "mode", "enabledWhen" })
            {
                Require(!string.IsNullOrWhiteSpace(state[required]?.GetValue<string>()), $"{stateKey} lacks {required}.", errors);
            }

            JsonArray nested = RequireArray(state, "nestedControls", errors);
            JsonObject[] controls = nested.OfType<JsonObject>().ToArray();
            Require(controls.Length == 1, $"{stateKey} must contain exactly one nested control descriptor.", errors);
            if (ExpectedPrintNestedControlKeys.TryGetValue(stateKey, out string? expectedNested))
            {
                ValidateOrderedControls(state, "nestedControls", [expectedNested], errors);
            }
        }
    }

    private static void ValidateOrderedControls(JsonObject owner, string property, IReadOnlyList<string> expected, List<string> errors)
    {
        JsonArray controls = RequireArray(owner, property, errors);
        JsonObject[] objects = controls.OfType<JsonObject>().ToArray();
        string[] actual = objects.Select(control => control["mappingKey"]?.GetValue<string>() ?? string.Empty).ToArray();
        Require(actual.SequenceEqual(expected, StringComparer.Ordinal),
            $"{property} controls do not exactly match the source-derived inventory.", errors);
        RequireUnique(objects, "mappingKey", errors, property);
        int[] orders = objects.Select(control => control["order"]?.GetValue<int>() ?? -1).Order().ToArray();
        Require(orders.SequenceEqual(Enumerable.Range(0, orders.Length)), $"{property} order must be contiguous from zero.", errors);
        foreach (JsonObject control in objects)
        {
            foreach (string required in new[] { "kind", "defaultState", "action", "transition" })
            {
                Require(!string.IsNullOrWhiteSpace(control[required]?.GetValue<string>()), $"{property} control lacks {required}.", errors);
            }
        }
    }

    private static void RequireUnique(IEnumerable<JsonObject> objects, string property, List<string> errors, string? scope = null)
    {
        string[] values = objects.Select(item => item[property]?.GetValue<string>() ?? string.Empty).ToArray();
        Require(values.All(value => !string.IsNullOrWhiteSpace(value)), $"{scope ?? "collection"} contains a blank {property}.", errors);
        Require(values.Distinct(StringComparer.Ordinal).Count() == values.Length, $"{scope ?? "collection"} contains duplicate {property} values.", errors);
    }

    private static JsonArray RequireArray(JsonObject owner, string property, List<string> errors)
    {
        if (owner[property] is JsonArray array) return array;
        errors.Add($"{property} must be an array.");
        return [];
    }

    private static JsonObject RequireObject(JsonObject owner, string property, List<string> errors)
    {
        if (owner[property] is JsonObject obj) return obj;
        errors.Add($"{property} must be an object.");
        return [];
    }

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition) errors.Add(message);
    }
}
