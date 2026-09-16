namespace FrameWeb.Startup;

public sealed record ServiceStatus(string Status, string Message, bool Engine, bool Frontend);

public sealed class StartupState
{
    private ServiceStatus snapshot = new("starting", "ローカル環境を準備しています。", false, false);
    public ServiceStatus Snapshot => Volatile.Read(ref snapshot);
    public void Set(string status, string message, bool engine = false, bool frontend = false) =>
        Volatile.Write(ref snapshot, new(status, message, engine, frontend));
}
