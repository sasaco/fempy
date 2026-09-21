using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace FrameWeb.LocalRuntime;

internal static class WindowsTcpListenerOwner
{
    private const uint NoError = 0;
    private const uint ErrorInsufficientBuffer = 122;
    private const int MaximumTableBytes = 16 * 1024 * 1024;

    public static int? GetOwningProcessId(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("TCP listener ownership inspection requires Windows.");
        }

        if (!IPAddress.TryParse(endpoint.DnsSafeHost, out IPAddress? address) ||
            !IPAddress.IsLoopback(address))
        {
            throw new FrameWebRuntimeException(
                "FrameWeb listener ownership can only be verified for a literal loopback IP address.");
        }

        int[] owners = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => GetIpv4Owners(address, endpoint.Port),
            AddressFamily.InterNetworkV6 => GetIpv6Owners(address, endpoint.Port),
            _ => throw new FrameWebRuntimeException("The FrameWeb listener address family is unsupported."),
        };

        return owners.Length switch
        {
            0 => null,
            1 => owners[0],
            _ => throw new FrameWebRuntimeException(
                "FrameWeb listener ownership is ambiguous and cannot be authenticated safely."),
        };
    }

    private static int[] GetIpv4Owners(IPAddress expectedAddress, int expectedPort) =>
        ReadTable(AddressFamily.InterNetwork, static (buffer, count, address, port) =>
        {
            int rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            HashSet<int> owners = [];
            for (int index = 0; index < count; index++)
            {
                IntPtr rowPointer = IntPtr.Add(buffer, sizeof(uint) + (index * rowSize));
                MibTcpRowOwnerPid row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                if (row.State == MibTcpStateListen &&
                    DecodePort(row.LocalPort) == port &&
                    new IPAddress(row.LocalAddress).Equals(address) &&
                    row.OwningProcessId is > 0 and <= int.MaxValue)
                {
                    _ = owners.Add(checked((int)row.OwningProcessId));
                }
            }

            return owners.ToArray();
        }, expectedAddress, expectedPort);

    private static int[] GetIpv6Owners(IPAddress expectedAddress, int expectedPort) =>
        ReadTable(AddressFamily.InterNetworkV6, static (buffer, count, address, port) =>
        {
            int rowSize = Marshal.SizeOf<MibTcp6RowOwnerPid>();
            HashSet<int> owners = [];
            for (int index = 0; index < count; index++)
            {
                IntPtr rowPointer = IntPtr.Add(buffer, sizeof(uint) + (index * rowSize));
                MibTcp6RowOwnerPid row = Marshal.PtrToStructure<MibTcp6RowOwnerPid>(rowPointer);
                if (row.State == MibTcpStateListen &&
                    DecodePort(row.LocalPort) == port &&
                    new IPAddress(row.LocalAddress, row.LocalScopeId).Equals(address) &&
                    row.OwningProcessId is > 0 and <= int.MaxValue)
                {
                    _ = owners.Add(checked((int)row.OwningProcessId));
                }
            }

            return owners.ToArray();
        }, expectedAddress, expectedPort);

    private static int[] ReadTable(
        AddressFamily addressFamily,
        Func<IntPtr, int, IPAddress, int, int[]> readRows,
        IPAddress expectedAddress,
        int expectedPort)
    {
        int size = 0;
        uint result = GetExtendedTcpTable(
            IntPtr.Zero,
            ref size,
            order: false,
            (int)addressFamily,
            TcpTableClass.OwnerPidListener,
            reserved: 0);
        if (result != ErrorInsufficientBuffer && result != NoError)
        {
            throw new Win32Exception(checked((int)result), "Could not size the TCP listener ownership table.");
        }

        if (size < sizeof(uint) || size > MaximumTableBytes)
        {
            throw new FrameWebRuntimeException("The TCP listener ownership table size is invalid.");
        }

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetExtendedTcpTable(
                buffer,
                ref size,
                order: false,
                (int)addressFamily,
                TcpTableClass.OwnerPidListener,
                reserved: 0);
            if (result != NoError)
            {
                throw new Win32Exception(checked((int)result), "Could not inspect TCP listener ownership.");
            }

            int count = Marshal.ReadInt32(buffer);
            if (count < 0)
            {
                throw new FrameWebRuntimeException("The TCP listener ownership table count is invalid.");
            }

            return readRows(buffer, count, expectedAddress, expectedPort);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int DecodePort(uint value) =>
        checked((int)(((value & 0x000000ffU) << 8) | ((value & 0x0000ff00U) >> 8)));

    private const uint MibTcpStateListen = 2;

    private enum TcpTableClass
    {
        OwnerPidListener = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        public uint LocalPort;
        public uint RemoteAddress;
        public uint RemotePort;
        public uint OwningProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddress;

        public uint LocalScopeId;
        public uint LocalPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteAddress;

        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningProcessId;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        [MarshalAs(UnmanagedType.Bool)] bool order,
        int addressFamily,
        TcpTableClass tableClass,
        uint reserved);
}
