using System.Runtime.InteropServices;

namespace LinearPipe;

public static unsafe partial class Tsc
{
    private const int PROT_READ = 1;
    private const int PROT_WRITE = 2;
    private const int PROT_EXEC = 4;
    private const int MAP_PRIVATE = 2;
    private const int MAP_ANONYMOUS = 0x20;

    [LibraryImport("libc", SetLastError = true)]
    private static partial nint mmap(nint addr, nuint length, int prot, int flags, int fd, nint offset);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int mprotect(nint addr, nuint length, int prot);

    private static readonly byte[] BeginCode =
    {
        0x0f, 0xae, 0xe8,
        0x0f, 0x31,
        0x48, 0xc1, 0xe2, 0x20,
        0x48, 0x09, 0xd0,
        0xc3
    };

    private static readonly byte[] EndCode =
    {
        0x0f, 0x01, 0xf9,
        0x48, 0xc1, 0xe2, 0x20,
        0x48, 0x09, 0xd0,
        0x0f, 0xae, 0xe8,
        0xc3
    };

    private static readonly delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong> BeginFn =
        (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong>)Load(BeginCode);

    private static readonly delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong> EndFn =
        (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong>)Load(EndCode);

    public static ulong Begin()
    {
        return BeginFn();
    }

    public static ulong End()
    {
        return EndFn();
    }

    private static nint Load(byte[] code)
    {
        nint page = mmap(0,
                         4096,
                         PROT_READ | PROT_WRITE,
                         MAP_PRIVATE | MAP_ANONYMOUS,
                         -1,
                         0);
        if (page == -1)
        {
            throw new InvalidOperationException($"Tsc: mmap failed (errno {Marshal.GetLastSystemError()}).");
        }
        Marshal.Copy(code, 0, page, code.Length);
        if (mprotect(page, 4096, PROT_READ | PROT_EXEC) != 0)
        {
            throw new InvalidOperationException($"Tsc: mprotect failed (errno {Marshal.GetLastSystemError()}).");
        }
        return page;
    }
}
