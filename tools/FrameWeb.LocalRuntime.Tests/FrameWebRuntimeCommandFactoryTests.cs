namespace FrameWeb.LocalRuntime.Tests;

public sealed class FrameWebRuntimeCommandFactoryTests
{
    [Fact]
    public void Create_UsesUvLockedRunWhenVirtualEnvironmentIsAbsent()
    {
        using TemporaryRepository repository = new();
        FrameWebRuntimeOptions options = new(repository.Root)
        {
            UvExecutablePath = "C:\\tools\\uv.exe",
            Port = 8123,
        };

        FrameWebRuntimeCommand command = FrameWebRuntimeCommandFactory.Create(options);

        Assert.Equal("C:\\tools\\uv.exe", command.FileName);
        Assert.Equal(repository.Root, command.WorkingDirectory);
        Assert.Equal("--directory", command.Arguments[0]);
        Assert.Contains("--locked", command.Arguments);
        Assert.Equal("8123", command.Arguments[^1]);
        Assert.Equal("1", command.Environment["PYTHONUNBUFFERED"]);
        Assert.Null(command.Environment["ASPNETCORE_URLS"]);
        string token = Assert.IsType<string>(command.Environment["FRAMEWEB_LOCAL_AUTH_TOKEN"]);
        Assert.Equal(43, token.Length);
        Assert.All(token, character => Assert.True(
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.DoesNotContain(command.Arguments, argument => argument.Contains("Angular", StringComparison.OrdinalIgnoreCase));

        FrameWebRuntimeCommand second = FrameWebRuntimeCommandFactory.Create(options);
        Assert.NotEqual(token, second.Environment["FRAMEWEB_LOCAL_AUTH_TOKEN"]);
    }

    [Fact]
    public void Create_PrefersExplicitPythonExecutable()
    {
        using TemporaryRepository repository = new();
        string python = Path.Combine(repository.Root, "python.exe");
        FrameWebRuntimeCommand command = FrameWebRuntimeCommandFactory.Create(new FrameWebRuntimeOptions(repository.Root)
        {
            PythonExecutablePath = python,
        });

        Assert.Equal(python, command.FileName);
        Assert.Equal(Path.Combine(repository.Root, "FrameWeb"), command.WorkingDirectory);
        Assert.Equal("-m", command.Arguments[0]);
        Assert.Equal("flask", command.Arguments[1]);
    }

    [Fact]
    public void Create_RejectsNonLocalBindAndReadinessAddresses()
    {
        using TemporaryRepository repository = new();

        Assert.Throws<ArgumentException>(() => FrameWebRuntimeCommandFactory.Create(
            new FrameWebRuntimeOptions(repository.Root) { Host = "0.0.0.0" }));
        Assert.Throws<ArgumentException>(() => FrameWebRuntimeCommandFactory.Create(
            new FrameWebRuntimeOptions(repository.Root) { Host = "localhost" }));
        Assert.Throws<ArgumentException>(() => FrameWebRuntimeCommandFactory.Create(
            new FrameWebRuntimeOptions(repository.Root)
            {
                ReadinessUri = new Uri("https://example.invalid/", UriKind.Absolute),
            }));
        Assert.Throws<ArgumentException>(() => FrameWebRuntimeCommandFactory.Create(
            new FrameWebRuntimeOptions(repository.Root)
            {
                ReadinessUri = new Uri("http://127.0.0.1:8080/not-root", UriKind.Absolute),
            }));
    }
}
