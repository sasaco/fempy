using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace FrameWebforCsharp.Core.Shell;

public enum DockState
{
    Document = 1,
    DockLeft = 2,
    DockRight = 3,
    DockTop = 4,
    DockBottom = 5,
    Float = 6,
    Hidden = 7,
}

/// <summary>Persisted pixel bounds for a floating pane.</summary>
public readonly record struct WindowBounds
{
    public const int MaximumDimension = 100_000;
    public const int MaximumCoordinateMagnitude = 1_000_000;

    [JsonConstructor]
    public WindowBounds(int x, int y, int width, int height)
    {
        if (Math.Abs((long)x) > MaximumCoordinateMagnitude ||
            Math.Abs((long)y) > MaximumCoordinateMagnitude ||
            width is <= 0 or > MaximumDimension ||
            height is <= 0 or > MaximumDimension)
        {
            throw new ContractValidationException(
                ContractError.InvalidBounds,
                "Bounds require positive dimensions no larger than 100000 and coordinates within +/-1000000.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    [JsonPropertyName("x")]
    [JsonPropertyOrder(0)]
    public int X { get; }

    [JsonPropertyName("y")]
    [JsonPropertyOrder(1)]
    public int Y { get; }

    [JsonPropertyName("width")]
    [JsonPropertyOrder(2)]
    public int Width { get; }

    [JsonPropertyName("height")]
    [JsonPropertyOrder(3)]
    public int Height { get; }
}

/// <summary>Immutable persisted state for one whitelisted content key.</summary>
public sealed class LayoutContentState
{
    [JsonConstructor]
    public LayoutContentState(DocumentKey key, DockState dockState, WindowBounds? bounds, int order)
    {
        key.Validate();
        if (!Enum.IsDefined(dockState))
        {
            throw new ContractValidationException(
                ContractError.InvalidDockState,
                $"Dock state value '{dockState}' is not supported.",
                nameof(dockState));
        }

        if (order < 0)
        {
            throw new ContractValidationException(
                ContractError.InvalidOrder,
                "Layout order must be zero or greater.",
                nameof(order));
        }

        Key = key;
        DockState = dockState;
        Bounds = bounds;
        Order = order;
    }

    [JsonPropertyName("key")]
    [JsonPropertyOrder(0)]
    public DocumentKey Key { get; }

    [JsonPropertyName("dockState")]
    [JsonPropertyOrder(1)]
    public DockState DockState { get; }

    [JsonPropertyName("bounds")]
    [JsonPropertyOrder(2)]
    public WindowBounds? Bounds { get; }

    [JsonPropertyName("order")]
    [JsonPropertyOrder(3)]
    public int Order { get; }
}

/// <summary>Versioned immutable shell layout. Restore requires separate whitelist validation.</summary>
public sealed class LayoutState
{
    public const int CurrentVersion = 1;

    [JsonConstructor]
    public LayoutState(int version, IReadOnlyList<LayoutContentState> contents, DocumentKey? activeDocument)
    {
        if (version != CurrentVersion)
        {
            throw new UnknownContractVersionException(nameof(LayoutState), version, CurrentVersion);
        }

        ArgumentNullException.ThrowIfNull(contents);
        if (contents.Any(static content => content is null))
        {
            throw new ContractValidationException(
                ContractError.DuplicateContentKey,
                "Layout contents cannot contain null entries.",
                nameof(contents));
        }

        activeDocument?.Validate();
        Version = version;
        Contents = new ReadOnlyCollection<LayoutContentState>(contents.ToArray());
        ActiveDocument = activeDocument;
    }

    [JsonPropertyName("version")]
    [JsonPropertyOrder(0)]
    public int Version { get; }

    [JsonPropertyName("contents")]
    [JsonPropertyOrder(1)]
    public IReadOnlyList<LayoutContentState> Contents { get; }

    [JsonPropertyName("activeDocument")]
    [JsonPropertyOrder(2)]
    public DocumentKey? ActiveDocument { get; }
}
