using PDF_Manager.Core.Shell;

namespace PDF_Manager.Core.Tests;

public sealed class WindowLifetimePolicyTests
{
    [Theory]
    [InlineData(DocumentKind.Tool, WindowCloseAction.Hide)]
    [InlineData(DocumentKind.Document, WindowCloseAction.Dispose)]
    public void CloseAction_IsExplicitForEachContentKind(
        DocumentKind kind,
        WindowCloseAction expected)
    {
        DocumentKey key = new(DocumentKey.CurrentVersion, kind, "content:alpha");

        Assert.Equal(expected, WindowLifetimePolicy.GetCloseAction(key));
    }
}
