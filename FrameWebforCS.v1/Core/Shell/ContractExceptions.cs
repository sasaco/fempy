namespace FrameWebforCS.Core.Shell;

/// <summary>Identifies a stable validation failure at a persisted contract boundary.</summary>
public enum ContractError
{
    UnknownVersion,
    InvalidIdentifier,
    UnknownDocumentKind,
    InvalidBounds,
    InvalidOrder,
    InvalidDockState,
    DuplicateContentKey,
    ActiveDocumentInconsistent,
}

/// <summary>Reports invalid persisted state without depending on UI exception types.</summary>
public class ContractValidationException : ArgumentException
{
    public ContractValidationException(ContractError error, string message, string? paramName = null)
        : base(message, paramName)
    {
        Error = error;
    }

    public ContractError Error { get; }
}

/// <summary>Reports a persisted contract version that this process cannot restore.</summary>
public sealed class UnknownContractVersionException : ContractValidationException
{
    public UnknownContractVersionException(string contractName, int actualVersion, int supportedVersion)
        : base(
            ContractError.UnknownVersion,
            $"{contractName} version {actualVersion} is not supported; expected version {supportedVersion}.",
            nameof(actualVersion))
    {
        ContractName = contractName;
        ActualVersion = actualVersion;
        SupportedVersion = supportedVersion;
    }

    public string ContractName { get; }

    public int ActualVersion { get; }

    public int SupportedVersion { get; }
}

/// <summary>Reports an attempt to register a key more than once.</summary>
public sealed class DuplicateContentKeyException : InvalidOperationException
{
    public DuplicateContentKeyException(DocumentKey key)
        : base($"A content factory is already registered for '{key}'.")
    {
        Key = key;
    }

    public DocumentKey Key { get; }
}

/// <summary>Reports a restore/open request whose key is not in the explicit registry.</summary>
public sealed class UnknownContentKeyException : KeyNotFoundException
{
    public UnknownContentKeyException(DocumentKey key)
        : base($"No content factory is registered for '{key}'.")
    {
        Key = key;
    }

    public DocumentKey Key { get; }
}

/// <summary>Reports a registered factory that violates its non-null result contract.</summary>
public sealed class ContentFactoryException : InvalidOperationException
{
    public ContentFactoryException(DocumentKey key)
        : base($"The registered content factory for '{key}' returned null.")
    {
        Key = key;
    }

    public DocumentKey Key { get; }
}
