using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace FrameWebforCsharp.Printing;

public sealed class PrintFontUnavailableException : InvalidOperationException
{
    public PrintFontUnavailableException(
        PrintTextLanguage language,
        bool bold,
        string fileName,
        Exception innerException)
        : base(
            $"The installed font required for {language} {(bold ? "bold" : "regular")} printing is unavailable: {fileName}.",
            innerException)
    {
        Language = language;
        Bold = bold;
        FileName = fileName;
    }

    public PrintTextLanguage Language { get; }

    public bool Bold { get; }

    public string FileName { get; }
}

internal interface IInstalledFontDiscovery
{
    Stream OpenRead(string fileName);
}

internal sealed class WindowsInstalledFontDiscovery : IInstalledFontDiscovery
{
    private readonly string _fontDirectory;
    private readonly string _directoryPrefix;

    public WindowsInstalledFontDiscovery()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows))
        {
            throw new PlatformNotSupportedException("The Windows directory is unavailable for installed-font resolution.");
        }

        _fontDirectory = Path.GetFullPath(Path.Combine(windows, "Fonts"));
        _directoryPrefix = _fontDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _fontDirectory
            : _fontDirectory + Path.DirectorySeparatorChar;
    }

    public Stream OpenRead(string fileName)
    {
        string candidate = Path.GetFullPath(Path.Combine(_fontDirectory, fileName));
        if (!candidate.StartsWith(_directoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The installed-font path escaped the Windows Fonts directory.");
        }

        return new FileStream(
            candidate,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            FileOptions.SequentialScan);
    }
}

internal sealed class InstalledWindowsFontResolver : IFontResolver
{
    public const string EnglishFamily = "FrameWeb Installed English";
    public const string JapaneseFamily = "FrameWeb Installed Japanese";
    public const string ChineseFamily = "FrameWeb Installed Chinese";
    public const int MaximumFontBytes = 64 * 1024 * 1024;

    private const string EnglishRegularFace = "frameweb-arial-regular";
    private const string EnglishBoldFace = "frameweb-arial-bold";
    private const string JapaneseRegularFace = "frameweb-yumin-regular";
    private const string JapaneseBoldFace = "frameweb-yumin-bold";
    private const string ChineseRegularFace = "frameweb-msyh-regular";
    private static readonly IReadOnlyDictionary<string, FontFace> Faces = CreateFaces();
    private readonly IInstalledFontDiscovery _discovery;
    private readonly ConcurrentDictionary<string, Lazy<byte[]>> _fontData = new(StringComparer.Ordinal);

    public InstalledWindowsFontResolver()
        : this(new WindowsInstalledFontDiscovery())
    {
    }

    internal InstalledWindowsFontResolver(IInstalledFontDiscovery discovery)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        string? faceName = familyName switch
        {
            EnglishFamily => bold ? EnglishBoldFace : EnglishRegularFace,
            JapaneseFamily => bold ? JapaneseBoldFace : JapaneseRegularFace,
            ChineseFamily => ChineseRegularFace,
            _ => null,
        };
        return faceName is null
            ? null
            : new FontResolverInfo(
                faceName,
                mustSimulateBold: familyName == ChineseFamily && bold,
                mustSimulateItalic: italic);
    }

    public byte[]? GetFont(string faceName)
    {
        ArgumentNullException.ThrowIfNull(faceName);
        if (!Faces.TryGetValue(faceName, out FontFace? face))
        {
            return null;
        }

        return _fontData.GetOrAdd(
            faceName,
            _ => new Lazy<byte[]>(
                () => LoadFont(face),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    internal void EnsureLanguage(PrintTextLanguage language)
    {
        _ = GetFont(GetFaceName(language, bold: false));
        _ = GetFont(GetFaceName(language, bold: true));
    }

    private byte[] LoadFont(FontFace face)
    {
        try
        {
            using Stream source = _discovery.OpenRead(face.FileName);
            if (!source.CanRead || !source.CanSeek)
            {
                throw new InvalidDataException("The installed font source must be a readable, seekable stream.");
            }

            long length = source.Length;
            if (length is <= 0 or > MaximumFontBytes || length > int.MaxValue)
            {
                throw new InvalidDataException($"The installed font exceeds the {MaximumFontBytes}-byte input limit.");
            }

            byte[] installedFont = new byte[(int)length];
            source.Position = 0;
            source.ReadExactly(installedFont);
            if (source.ReadByte() != -1)
            {
                throw new InvalidDataException("The installed font changed while it was being read.");
            }

            return face.IsCollection
                ? TrueTypeCollectionExtractor.ExtractFace(installedFont, face.FaceIndex)
                : installedFont;
        }
        catch (PrintFontUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new PrintFontUnavailableException(
                face.Language,
                face.Bold,
                face.FileName,
                exception);
        }
    }

    private static string GetFaceName(PrintTextLanguage language, bool bold) => language switch
    {
        PrintTextLanguage.English => bold ? EnglishBoldFace : EnglishRegularFace,
        PrintTextLanguage.Japanese => bold ? JapaneseBoldFace : JapaneseRegularFace,
        PrintTextLanguage.SimplifiedChinese => ChineseRegularFace,
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown print language."),
    };

    private static IReadOnlyDictionary<string, FontFace> CreateFaces() =>
        new Dictionary<string, FontFace>(StringComparer.Ordinal)
        {
            [EnglishRegularFace] = new(PrintTextLanguage.English, Bold: false, "arial.ttf", IsCollection: false, 0),
            [EnglishBoldFace] = new(PrintTextLanguage.English, Bold: true, "arialbd.ttf", IsCollection: false, 0),
            [JapaneseRegularFace] = new(PrintTextLanguage.Japanese, Bold: false, "yumin.ttf", IsCollection: false, 0),
            [JapaneseBoldFace] = new(PrintTextLanguage.Japanese, Bold: true, "yumindb.ttf", IsCollection: false, 0),
            [ChineseRegularFace] = new(PrintTextLanguage.SimplifiedChinese, Bold: false, "msyh.ttc", IsCollection: true, 0),
        };

    private sealed record FontFace(
        PrintTextLanguage Language,
        bool Bold,
        string FileName,
        bool IsCollection,
        int FaceIndex);
}
