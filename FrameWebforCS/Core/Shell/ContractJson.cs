using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameWebforCS.Core.Shell;

internal static class ContractJson
{
    internal static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = false,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}

public static class DocumentKeyJson
{
    public static string Serialize(DocumentKey key)
    {
        key.Validate();
        return JsonSerializer.Serialize(key, ContractJson.Options);
    }

    public static DocumentKey Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<DocumentKey>(json, ContractJson.Options);
    }
}

internal sealed class DocumentKeyJsonConverter : JsonConverter<DocumentKey>
{
    public override DocumentKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("DocumentKey must be a JSON object.");
        }

        HashSet<string> names = [];
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new JsonException($"Duplicate DocumentKey member '{property.Name}'.");
            }

            if (property.Name is not "version" and not "kind" and not "identifier")
            {
                throw new JsonException($"Unknown DocumentKey member '{property.Name}'.");
            }
        }

        int version = ReadRequiredInt32(root, "version");
        string kindText = ReadRequiredString(root, "kind");
        string identifier = ReadRequiredString(root, "identifier");
        DocumentKind kind = kindText switch
        {
            "tool" => DocumentKind.Tool,
            "document" => DocumentKind.Document,
            _ => throw new ContractValidationException(
                ContractError.UnknownDocumentKind,
                $"Document kind '{kindText}' is not supported.",
                "kind"),
        };

        return new DocumentKey(version, kind, identifier);
    }

    public override void Write(Utf8JsonWriter writer, DocumentKey value, JsonSerializerOptions options)
    {
        value.Validate();
        writer.WriteStartObject();
        writer.WriteNumber("version", value.Version);
        writer.WriteString("kind", value.Kind == DocumentKind.Tool ? "tool" : "document");
        writer.WriteString("identifier", value.Identifier);
        writer.WriteEndObject();
    }

    private static int ReadRequiredInt32(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element) || !element.TryGetInt32(out int value))
        {
            throw new JsonException($"DocumentKey member '{name}' must be an integer.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"DocumentKey member '{name}' must be a string.");
        }

        return element.GetString()!;
    }
}
