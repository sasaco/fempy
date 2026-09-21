using System.Security.Cryptography;

namespace FrameWeb.LocalRuntime;

public static class FrameWebLocalAuthentication
{
    public const string HeaderName = "X-FrameWeb-Local-Token";

    internal const string EnvironmentVariableName = "FRAMEWEB_LOCAL_AUTH_TOKEN";

    internal static string CreateToken()
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
