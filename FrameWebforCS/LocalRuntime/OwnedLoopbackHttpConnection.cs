using System.Net;
using System.Net.Sockets;

namespace FrameWeb.LocalRuntime;

internal static class OwnedLoopbackHttpConnection
{
    public static SocketsHttpHandler CreateHandler(Uri endpoint, Action requireOwnedListener)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(requireOwnedListener);
        if (!IPAddress.TryParse(endpoint.DnsSafeHost, out IPAddress? address) || !IPAddress.IsLoopback(address))
        {
            throw new FrameWebRuntimeException(
                "FrameWeb HTTP connections require a literal loopback IP address.");
        }

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            PooledConnectionLifetime = TimeSpan.Zero,
            ConnectCallback = async (context, cancellationToken) =>
            {
                if (!string.Equals(context.DnsEndPoint.Host, endpoint.DnsSafeHost, StringComparison.OrdinalIgnoreCase) ||
                    context.DnsEndPoint.Port != endpoint.Port)
                {
                    throw new FrameWebRuntimeException(
                        "FrameWeb HTTP connection redirection is not permitted.");
                }

                Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken)
                        .ConfigureAwait(false);
                    requireOwnedListener();
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }
}
