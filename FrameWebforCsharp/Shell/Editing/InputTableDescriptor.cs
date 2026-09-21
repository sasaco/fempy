using FrameWebforCsharp.Rendering.Scene;

namespace FrameWebforCsharp.Shell.Editing;

public enum InputTableKey
{
    ModelSettings,
    Nodes,
    Members,
    RigidZones,
    ElementPropertySets,
    Supports,
    SupportSets,
    Sections,
    Panels,
    Joints,
    JointReleaseSets,
    NoticePoints,
    MemberSprings,
    MemberSpringSets,
    LoadCases,
    NodalLoads,
    PrescribedDisplacements,
    MemberLoads,
    Define,
    Combine,
    Pickup,
}

public enum InputColumnKind
{
    Text,
    Number,
    Boolean,
    Choice,
}

public sealed record InputColumnDescriptor(
    string Id,
    string HeaderResourceKey,
    InputColumnKind Kind = InputColumnKind.Text,
    bool IsReadOnly = false);

public sealed record InputTableDescriptor(
    InputTableKey Key,
    string TitleResourceKey,
    string GridName,
    IReadOnlyList<InputColumnDescriptor> Columns);

public sealed record InputGridRow(
    string Id,
    IReadOnlyList<object?> Values,
    SceneEntityKey? SceneKey = null);

public sealed record InputGridRowUpdate(string Id, IReadOnlyList<string> Cells);

public enum InputGridFailure
{
    InvalidValue,
    ClipboardUnavailable,
    ClipboardTooLarge,
    ClipboardInvalidShape,
    ReadOnlyTarget,
    DeleteRejected,
}
