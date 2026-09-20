using System.Text.Json;

namespace PDF_Manager.RendererProbe;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            CommandLineOptions options = CommandLineOptions.Parse(args);
            if (options.Verify)
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            }

            ApplicationConfiguration.Initialize();
            if (options.Verify)
            {
                VerificationReport report = RendererVerification.Run(options.Cycles);
                Console.WriteLine(JsonSerializer.Serialize(report));
                return 0;
            }

            Application.Run(new RendererProbeShell(verificationMode: false));
            return 0;
        }
        catch (CommandLineException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }
}

internal sealed record CommandLineOptions(bool Verify, int Cycles)
{
    private const int DefaultCycles = 100;

    public static CommandLineOptions Parse(string[] args)
    {
        bool verify = false;
        int cycles = DefaultCycles;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--verify", StringComparison.OrdinalIgnoreCase))
            {
                verify = true;
                continue;
            }

            if (argument.StartsWith("--cycles=", StringComparison.OrdinalIgnoreCase))
            {
                cycles = ParseCycles(argument["--cycles=".Length..]);
                continue;
            }

            if (string.Equals(argument, "--cycles", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                cycles = ParseCycles(args[++index]);
                continue;
            }

            throw new CommandLineException($"Unknown argument: {argument}");
        }

        if (!verify && cycles != DefaultCycles)
        {
            throw new CommandLineException("--cycles requires --verify.");
        }

        return new CommandLineOptions(verify, cycles);
    }

    private static int ParseCycles(string value)
    {
        if (!int.TryParse(value, out int cycles) || cycles <= 0)
        {
            throw new CommandLineException("--cycles must be a positive integer.");
        }

        return cycles;
    }
}

internal sealed class CommandLineException(string message) : Exception(message);
