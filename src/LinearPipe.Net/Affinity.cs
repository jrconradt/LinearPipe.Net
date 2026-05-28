using System.Runtime.InteropServices;

namespace LinearPipe;

internal static partial class Affinity
{
    [LibraryImport("kernel32.dll")]
    private static partial UIntPtr SetThreadAffinityMask(IntPtr thread, UIntPtr mask);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentThread();

    [LibraryImport("libc", EntryPoint = "sched_setaffinity")]
    private static partial int SchedSetAffinity(int pid, int cpuSetSize, ref ulong mask);

    public static void PinCurrent(int coreIndex)
    {
        if (coreIndex < 0)
        {
            return;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (coreIndex >= 64)
            {
                return;
            }
            SetThreadAffinityMask(GetCurrentThread(), (UIntPtr)(1UL << coreIndex));
            return;
        }
        int words = (coreIndex >> 6) + 1;
        Span<ulong> mask = stackalloc ulong[words];
        mask.Clear();
        mask[coreIndex >> 6] = 1UL << (coreIndex & 63);
        SchedSetAffinity(0, words * sizeof(ulong), ref mask[0]);
    }
}
