namespace LinearPipe;

using System.Runtime.InteropServices;
using System.Security;

internal static unsafe class Native
{
    internal const int O_RDONLY = 0x0;
    internal const int O_RDWR = 0x2;
    internal const int O_CREAT = 0x40;
    internal const int O_TRUNC = 0x200;

    internal const int PROT_READ = 0x1;
    internal const int PROT_WRITE = 0x2;
    internal const int MAP_SHARED = 0x01;

    internal const int MADV_POPULATE_READ = 22;
    internal const int MADV_POPULATE_WRITE = 23;

    internal const int SEEK_END = 2;

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern int open(string path,
                                    int flags,
                                    int mode);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern int close(int fd);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern long lseek(int fd,
                                      long offset,
                                      int whence);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern int ftruncate(int fd,
                                         long length);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern nint mmap(nint addr,
                                     nuint length,
                                     int prot,
                                     int flags,
                                     int fd,
                                     nint offset);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern int munmap(nint addr,
                                      nuint length);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern int madvise(nint addr,
                                       nuint length,
                                       int advice);

    [SuppressUnmanagedCodeSecurity]
    [DllImport(dllName: "libc", SetLastError = true)]
    internal static extern int sched_setaffinity(int pid,
                                                 nuint cpusetsize,
                                                 byte* mask);
}
