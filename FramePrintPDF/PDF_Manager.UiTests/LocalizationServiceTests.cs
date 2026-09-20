using System.Resources;
using PDF_Manager.Resources;

namespace PDF_Manager.UiTests;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData(UiLanguage.Japanese, "ja", "ファイル(&F)", "プロジェクト ナビゲーション")]
    [InlineData(UiLanguage.English, "en", "&File", "Project Navigation")]
    [InlineData(UiLanguage.Chinese, "zh", "文件(&F)", "项目导航")]
    public void EachSupportedLanguage_ResolvesShellCaptions(
        UiLanguage language,
        string cultureName,
        string fileMenu,
        string navigationPane)
    {
        LocalizationService localization = new(language);

        Assert.Equal(language, localization.Language);
        Assert.Equal(cultureName, localization.Culture.Name);
        Assert.Equal(fileMenu, localization["MenuFile"]);
        Assert.Equal(navigationPane, localization["PaneNavigation"]);
        Assert.False(string.IsNullOrWhiteSpace(localization["PaneEditor"]));
        Assert.False(string.IsNullOrWhiteSpace(localization["PaneDiagnostics"]));
        Assert.False(string.IsNullOrWhiteSpace(localization["PaneViewport"]));
    }

    [Fact]
    public void RuntimeLanguageChange_RaisesOneNotificationOnlyWhenLanguageChanges()
    {
        LocalizationService localization = new(UiLanguage.English);
        int notifications = 0;
        localization.CultureChanged += (_, _) => notifications++;

        localization.SetLanguage(UiLanguage.Japanese);
        localization.SetLanguage(UiLanguage.Japanese);
        localization.SetLanguage(UiLanguage.Chinese);

        Assert.Equal(2, notifications);
        Assert.Equal(UiLanguage.Chinese, localization.Language);
        Assert.Equal("文件(&F)", localization["MenuFile"]);
    }

    [Fact]
    public void UnknownResourceKey_FailsFast()
    {
        LocalizationService localization = new(UiLanguage.English);

        Assert.Throws<MissingManifestResourceException>(() => localization["MissingShellCaption"]);
    }
}
