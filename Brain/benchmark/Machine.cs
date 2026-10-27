using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace Brain.Benchmark;

/// <summary>Describes and settles the machine so results say what they ran on and vary less between runs.</summary>
internal static class Machine
{
    /// <summary>
    ///     <para>
    ///         Pin the whole process to one logical core so the OS doesn't migrate it mid-execution, e.g., from a P-core to
    ///         an E-core on hybrid Intel chips. Works only on Linux and Windows
    ///     </para>
    /// </summary>
    public static void Pin(int cpu)
    {
        // written as if/else rather than early throw since platform analyzer (CA1416) can't follow
        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            Process.GetCurrentProcess().ProcessorAffinity = (nint)(1L << cpu);
        else
            throw new PlatformNotSupportedException("--cpu only works on Windows and Linux");
    }

    /// <summary>
    ///     Ask the OS to preempt this process less. Raising priority could fail due to insufficient permissions in an
    ///     unelevated console.
    /// </summary>
    public static void RaisePriority()
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        Thread.CurrentThread.Priority             = ThreadPriority.Highest;
    }

    public static string CpuName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                        "ProcessorNameString",
                        null
                    ) is string s)
                    return s;
            }

            if (OperatingSystem.IsLinux())
            {
                if (File.ReadLines("/proc/cpuinfo").FirstOrDefault(line => line.StartsWith("model name"))?.Split(':', 2)[1]
                        .Trim() is { } s)
                    return s;
            }
        }
        catch (Exception)
        {
            // fall through
        }

        return "unknown";
    }
}