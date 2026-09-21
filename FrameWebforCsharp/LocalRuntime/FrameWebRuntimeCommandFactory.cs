namespace FrameWeb.LocalRuntime;

public static class FrameWebRuntimeCommandFactory
{
    public static FrameWebRuntimeCommand Create(FrameWebRuntimeOptions options)
        => Create(options, FrameWebLocalAuthentication.CreateToken());

    internal static FrameWebRuntimeCommand Create(
        FrameWebRuntimeOptions options,
        string authenticationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationToken);
        options.Validate();
        if (options.CommandOverride is not null)
        {
            return WithAuthentication(options.CommandOverride, authenticationToken);
        }

        string frameWebDirectory = Path.Combine(options.RepositoryRoot, "FrameWeb");
        if (!File.Exists(Path.Combine(frameWebDirectory, "main.py")))
        {
            throw new DirectoryNotFoundException("Repository root must contain FrameWeb/main.py.");
        }

        Dictionary<string, string?> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PYTHONUTF8"] = "1",
            ["PYTHONUNBUFFERED"] = "1",
            ["ASPNETCORE_URLS"] = null,
            [FrameWebLocalAuthentication.EnvironmentVariableName] = authenticationToken,
        };
        string[] flaskArguments =
        [
            "-m", "flask", "--app", "main:app", "run",
            "--host", options.Host,
            "--port", options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ];

        if (!string.IsNullOrWhiteSpace(options.PythonExecutablePath))
        {
            return new FrameWebRuntimeCommand(
                Path.GetFullPath(options.PythonExecutablePath),
                frameWebDirectory,
                flaskArguments,
                environment);
        }

        string virtualEnvironmentPython = Path.Combine(frameWebDirectory, ".venv", "Scripts", "python.exe");
        if (File.Exists(virtualEnvironmentPython))
        {
            return new FrameWebRuntimeCommand(
                virtualEnvironmentPython,
                frameWebDirectory,
                flaskArguments,
                environment);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(options.UvExecutablePath);
        string[] uvArguments =
        [
            "--directory", frameWebDirectory,
            "run", "--locked", "python",
            .. flaskArguments,
        ];
        return new FrameWebRuntimeCommand(
            options.UvExecutablePath,
            options.RepositoryRoot,
            uvArguments,
            environment);
    }

    private static FrameWebRuntimeCommand WithAuthentication(
        FrameWebRuntimeCommand command,
        string authenticationToken)
    {
        Dictionary<string, string?> environment = new(command.Environment, StringComparer.OrdinalIgnoreCase)
        {
            [FrameWebLocalAuthentication.EnvironmentVariableName] = authenticationToken,
        };
        return new FrameWebRuntimeCommand(
            command.FileName,
            command.WorkingDirectory,
            command.Arguments,
            environment);
    }
}
