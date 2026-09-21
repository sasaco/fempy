using System.Text.Json.Serialization;

namespace FrameWebforCsharp.Core.Shell;

/// <summary>Classifies stable shell content without using captions or runtime type names.</summary>
public enum DocumentKind
{
    Tool = 1,
    Document = 2,
}

/// <summary>
/// Stable, versioned identity for shell content. Identifiers are invariant ASCII tokens and
/// must be selected by the composition root rather than derived from display text.
/// </summary>
[JsonConverter(typeof(DocumentKeyJsonConverter))]
public readonly record struct DocumentKey
{
    public const int CurrentVersion = 1;
    public const int MaximumIdentifierLength = 128;

    public DocumentKey(int version, DocumentKind kind, string identifier)
    {
        ValidateVersion(version);
        ValidateKind(kind);
        ValidateIdentifier(identifier);

        Version = version;
        Kind = kind;
        Identifier = identifier;
    }

    [JsonPropertyName("version")]
    [JsonPropertyOrder(0)]
    public int Version { get; }

    [JsonPropertyName("kind")]
    [JsonPropertyOrder(1)]
    public DocumentKind Kind { get; }

    [JsonPropertyName("identifier")]
    [JsonPropertyOrder(2)]
    public string Identifier { get; }

    public static DocumentKey Tool(string identifier) =>
        new(CurrentVersion, DocumentKind.Tool, identifier);

    public static DocumentKey Document(string identifier) =>
        new(CurrentVersion, DocumentKind.Document, identifier);

    /// <summary>Validates values even when a default struct instance crosses a boundary.</summary>
    public void Validate()
    {
        ValidateVersion(Version);
        ValidateKind(Kind);
        ValidateIdentifier(Identifier);
    }

    public override string ToString() => $"v{Version}:{Kind.ToString().ToLowerInvariant()}:{Identifier}";

    private static void ValidateVersion(int version)
    {
        if (version != CurrentVersion)
        {
            throw new UnknownContractVersionException(nameof(DocumentKey), version, CurrentVersion);
        }
    }

    private static void ValidateKind(DocumentKind kind)
    {
        if (kind is not DocumentKind.Tool and not DocumentKind.Document)
        {
            throw new ContractValidationException(
                ContractError.UnknownDocumentKind,
                $"Document kind value '{kind}' is not supported.",
                nameof(kind));
        }
    }

    private static void ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || identifier.Length > MaximumIdentifierLength)
        {
            throw InvalidIdentifier();
        }

        bool previousWasSeparator = false;
        for (int index = 0; index < identifier.Length; index++)
        {
            char character = identifier[index];
            bool isLetterOrDigit = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            bool isSeparator = character is '-' or '_' or '.' or ':';
            if ((!isLetterOrDigit && !isSeparator) ||
                (isSeparator && (index == 0 || index == identifier.Length - 1 || previousWasSeparator)))
            {
                throw InvalidIdentifier();
            }

            previousWasSeparator = isSeparator;
        }
    }

    private static ContractValidationException InvalidIdentifier() =>
        new(
            ContractError.InvalidIdentifier,
            "A document identifier must be 1-128 lowercase ASCII letters/digits separated by single '-', '_', '.', or ':' characters.",
            "identifier");
}
