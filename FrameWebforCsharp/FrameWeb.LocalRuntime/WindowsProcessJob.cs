using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FrameWeb.LocalRuntime;

internal sealed class WindowsProcessJob : IDisposable
{
    private const uint KillOnJobClose = 0x2000;
    private readonly SafeFileHandle _handle;
    private int _disposed;

    public WindowsProcessJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The FrameWeb local runtime requires Windows process jobs.");
        }

        _handle = CreateJobObject(IntPtr.Zero, null);
        if (_handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        ExtendedLimitInformation limits = new()
        {
            BasicLimitInformation = new BasicLimitInformation { LimitFlags = KillOnJobClose },
        };
        if (!SetInformationJobObject(
                _handle,
                JobObjectInfoType.ExtendedLimitInformation,
                ref limits,
                (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
        {
            int error = Marshal.GetLastWin32Error();
            _handle.Dispose();
            throw new Win32Exception(error);
        }
    }

    public void Add(Process process)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(process);
        if (!AssignProcessToJobObject(_handle, process.Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not manage the child process lifetime.");
        }
    }

    public int[] SnapshotProcessIds()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        const int maximumProcessCount = 4096;
        int headerBytes = sizeof(uint) * 2;
        int bufferBytes = checked(headerBytes + (maximumProcessCount * IntPtr.Size));
        IntPtr buffer = Marshal.AllocHGlobal(bufferBytes);
        try
        {
            if (!QueryInformationJobObject(
                    _handle,
                    JobObjectInfoType.BasicProcessIdList,
                    buffer,
                    (uint)bufferBytes,
                    out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not inspect the owned process job.");
            }

            uint count = unchecked((uint)Marshal.ReadInt32(buffer, sizeof(uint)));
            if (count > maximumProcessCount)
            {
                throw new InvalidOperationException("The owned process job exceeded its diagnostic process limit.");
            }

            int[] processIds = new int[count];
            for (int index = 0; index < processIds.Length; index++)
            {
                long processId = IntPtr.Size == sizeof(long)
                    ? Marshal.ReadInt64(buffer, headerBytes + (index * IntPtr.Size))
                    : unchecked((uint)Marshal.ReadInt32(buffer, headerBytes + (index * IntPtr.Size)));
                processIds[index] = checked((int)processId);
            }

            return processIds;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public bool ContainsProcessId(int processId) => SnapshotProcessIds().Contains(processId);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _handle.Dispose();
        }
    }

    private enum JobObjectInfoType
    {
        BasicProcessIdList = 3,
        ExtendedLimitInformation = 9,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        SafeFileHandle job,
        JobObjectInfoType infoClass,
        ref ExtendedLimitInformation info,
        uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(
        SafeFileHandle job,
        JobObjectInfoType infoClass,
        IntPtr info,
        uint length,
        out uint returnLength);
}
