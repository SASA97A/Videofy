using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Video_Size_Optimizer.Services;

/// <summary>
/// Makes sure ffmpeg and ffprobe do not keep running after Videofy ends.
/// Without this, a crash or a kill (Task Manager, kill, logout) left ffmpeg encoding in the
/// background with no window, using the CPU and writing a file nobody would finish.
/// Windows: every child is put in a job object that the system closes when Videofy ends, for any
/// reason (crash and Task Manager included), and closing it ends all children.
/// Linux and macOS: children are ended when Videofy gets SIGTERM, SIGINT, SIGHUP or SIGQUIT, on
/// normal exit and on an unhandled exception. kill -9 cannot be caught there.
/// </summary>
public static class ChildProcessGuard
{
    private static readonly object _lock = new();
    private static readonly HashSet<Process> _tracked = new();
    // Kept so the registrations are not collected.
    private static readonly List<PosixSignalRegistration> _signalRegistrations = new();
    private static IntPtr _job = IntPtr.Zero;
    private static bool _initialized;

    /// <summary>Call once at startup, before any child process is started.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        if (OperatingSystem.IsWindows())
        {
            _job = CreateKillOnCloseJob();
        }
        else
        {
            foreach (var signal in new[] { PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGHUP, PosixSignal.SIGQUIT })
            {
                try
                {
                    // The handler does not cancel the signal, so the app still ends as usual afterwards.
                    _signalRegistrations.Add(PosixSignalRegistration.Create(signal, _ => KillAll()));
                }
                catch
                {
                    // Not supported on this platform: rely on ProcessExit below.
                }
            }
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => KillAll();
    }

    /// <summary>Registers a started child process so it ends together with Videofy.</summary>
    public static void Track(Process? process)
    {
        if (process == null) return;

        if (OperatingSystem.IsWindows() && _job != IntPtr.Zero)
        {
            try
            {
                // Fails only if the process has already exited; then there is nothing to end.
                AssignProcessToJobObject(_job, process.Handle);
            }
            catch
            {
            }
        }

        lock (_lock) _tracked.Add(process);
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => { lock (_lock) _tracked.Remove(process); };
            if (process.HasExited) lock (_lock) _tracked.Remove(process);
        }
        catch
        {
            lock (_lock) _tracked.Remove(process);
        }
    }

    /// <summary>Ends every tracked child. No logging: this runs while the app is shutting down.</summary>
    private static void KillAll()
    {
        Process[] processes;
        lock (_lock)
        {
            processes = new Process[_tracked.Count];
            _tracked.CopyTo(processes);
            _tracked.Clear();
        }

        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch
            {
                // Already exited or disposed.
            }
        }
    }

    private static IntPtr CreateKillOnCloseJob()
    {
        try
        {
            IntPtr job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) return IntPtr.Zero;

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)length))
            {
                CloseHandle(job);
                return IntPtr.Zero;
            }

            // The handle stays open on purpose. Windows closes it when Videofy ends, which ends the children.
            return job;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
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
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass,
        ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
