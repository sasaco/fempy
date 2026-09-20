using System.Text.Json;

namespace PDF_Manager.Core.Shell;

public interface IContentKeyWhitelist
{
    bool IsRegistered(DocumentKey key);
}

public static class LayoutStateValidator
{
    /// <summary>
    /// Validates ordering, lifetime constraints, active-document consistency, and every key
    /// against the explicit registry used by the composition root.
    /// </summary>
    public static void Validate(LayoutState state, IContentKeyWhitelist whitelist)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(whitelist);
        if (state.Version != LayoutState.CurrentVersion)
        {
            throw new UnknownContractVersionException(
                nameof(LayoutState),
                state.Version,
                LayoutState.CurrentVersion);
        }

        HashSet<DocumentKey> keys = [];
        for (int index = 0; index < state.Contents.Count; index++)
        {
            LayoutContentState content = state.Contents[index];
            content.Key.Validate();
            if (!whitelist.IsRegistered(content.Key))
            {
                throw new UnknownContentKeyException(content.Key);
            }

            if (!keys.Add(content.Key))
            {
                throw new ContractValidationException(
                    ContractError.DuplicateContentKey,
                    $"Layout contains the key '{content.Key}' more than once.");
            }

            if (content.Order != index)
            {
                throw new ContractValidationException(
                    ContractError.InvalidOrder,
                    "Layout order must be unique, contiguous, and match persisted content order.");
            }

            ValidateDockState(content);
        }

        ValidateActiveDocument(state, keys);
    }

    private static void ValidateDockState(LayoutContentState content)
    {
        if (content.DockState == DockState.Float && content.Bounds is null)
        {
            throw new ContractValidationException(
                ContractError.InvalidBounds,
                $"Floating content '{content.Key}' requires bounds.");
        }

        bool validForKind = content.Key.Kind switch
        {
            DocumentKind.Tool => content.DockState != DockState.Document,
            DocumentKind.Document => content.DockState is DockState.Document or DockState.Float,
            _ => false,
        };
        if (!validForKind)
        {
            throw new ContractValidationException(
                ContractError.InvalidDockState,
                $"Dock state '{content.DockState}' is not valid for '{content.Key.Kind}'.");
        }
    }

    private static void ValidateActiveDocument(LayoutState state, HashSet<DocumentKey> keys)
    {
        if (state.ActiveDocument is not DocumentKey active)
        {
            return;
        }

        if (active.Kind != DocumentKind.Document || !keys.Contains(active))
        {
            throw new ContractValidationException(
                ContractError.ActiveDocumentInconsistent,
                "The active document must be a persisted document key present in layout contents.");
        }
    }
}

public static class LayoutStateJson
{
    public static string Serialize(LayoutState state, IContentKeyWhitelist whitelist)
    {
        LayoutStateValidator.Validate(state, whitelist);
        return JsonSerializer.Serialize(state, ContractJson.Options);
    }

    public static LayoutState Deserialize(string json, IContentKeyWhitelist whitelist)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(whitelist);
        LayoutState state = JsonSerializer.Deserialize<LayoutState>(json, ContractJson.Options)
            ?? throw new JsonException("Layout JSON cannot be null.");
        LayoutStateValidator.Validate(state, whitelist);
        return state;
    }
}
