using System.Runtime.InteropServices;
using LinearPipe;
using Xunit;

namespace LinearPipeTests;

public unsafe partial class LinearPipelineTests
{
    [LibraryImport("libc", SetLastError = true)]
    private static partial nint mmap(nint addr, nuint length, int prot, int flags, int fd, nint offset);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int munmap(nint addr, nuint length);

    private const int ProtRead = 0x1;
    private const int ProtWrite = 0x2;
    private const int ProtExec = 0x4;
    private const int MapPrivate = 0x2;
    private const int MapAnonymous = 0x20;

    private static readonly byte[] IdentityCode =
    {
        0xb9, 0x40, 0x00, 0x00, 0x00,
        0xf3, 0xa4,
        0xc3
    };

    private static readonly byte[] XorConstsCode =
    {
        0x31, 0xc9,
        0x8a, 0x04, 0x0e,
        0x32, 0x04, 0x11,
        0x88, 0x04, 0x0f,
        0xff, 0xc1,
        0x83, 0xf9, 0x40,
        0x75, 0xf0,
        0xc3
    };

    private static byte* MapCode(byte[] code)
    {
        nint p = mmap(0, 4096, ProtRead | ProtWrite | ProtExec, MapPrivate | MapAnonymous, -1, 0);
        Assert.NotEqual(-1, p);
        Marshal.Copy(code, 0, p, code.Length);
        return (byte*)p;
    }

    private static string WriteSource(byte[] data)
    {
        string path = Path.Combine(Path.GetTempPath(), $"linearpipe-src-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public void Identity_Reproduces_Source()
    {
        byte[] data = new byte[64 * 50];
        new Random(11).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null))
        {
            pipe.Flow();
        }
        munmap((nint)code, 4096);

        byte[] result = File.ReadAllBytes(dst);
        Assert.Equal(data, result);

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Pool_Reused_Across_Flows()
    {
        byte[] data = new byte[64 * 40];
        new Random(5).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null))
        {
            pipe.Flow();
            pipe.Flow();
        }
        munmap((nint)code, 4096);

        byte[] result = File.ReadAllBytes(dst);
        Assert.Equal(data, result);

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Xor_Transform_Applies_Per_Chunk()
    {
        byte[] data = new byte[64 * 37];
        new Random(99).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* consts = (byte*)Marshal.AllocHGlobal(64);
        for (int i = 0; i < 64; i++)
        {
            consts[i] = 0xA5;
        }

        byte* code = MapCode(XorConstsCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, consts))
        {
            pipe.Flow();
        }
        munmap((nint)code, 4096);
        Marshal.FreeHGlobal((nint)consts);

        byte[] result = File.ReadAllBytes(dst);
        byte[] expected = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            expected[i] = (byte)(data[i] ^ 0xA5);
        }
        Assert.Equal(expected, result);

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Partial_Tail_Chunk_Roundtrips()
    {
        byte[] data = new byte[64 * 5 + 13];
        new Random(7).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null))
        {
            pipe.Flow();
        }
        munmap((nint)code, 4096);

        byte[] result = File.ReadAllBytes(dst);
        Assert.Equal(data.Length, result.Length);
        Assert.Equal(data, result);

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Partial_Tail_Chunk_Xor_Applies()
    {
        byte[] data = new byte[64 * 3 + 40];
        new Random(13).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* consts = (byte*)Marshal.AllocHGlobal(64);
        for (int i = 0; i < 64; i++)
        {
            consts[i] = 0x5A;
        }

        byte* code = MapCode(XorConstsCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, consts))
        {
            pipe.Flow();
        }
        munmap((nint)code, 4096);
        Marshal.FreeHGlobal((nint)consts);

        byte[] result = File.ReadAllBytes(dst);
        byte[] expected = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            expected[i] = (byte)(data[i] ^ 0x5A);
        }
        Assert.Equal(data.Length, result.Length);
        Assert.Equal(expected, result);

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Empty_Source_Produces_Empty_Sink()
    {
        string src = WriteSource(Array.Empty<byte>());
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null))
        {
            pipe.Flow();
        }
        munmap((nint)code, 4096);

        Assert.True(File.Exists(dst));
        Assert.Empty(File.ReadAllBytes(dst));

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Fewer_Chunks_Than_Workers_Roundtrips()
    {
        byte[] data = new byte[64 * 3];
        new Random(21).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        var options = new PipelineOptions { WorkerCount = 10 };
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null, options))
        {
            pipe.Flow();
        }
        munmap((nint)code, 4096);

        Assert.Equal(data, File.ReadAllBytes(dst));

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Custom_AffinityCores_Roundtrips()
    {
        byte[] data = new byte[64 * 20];
        new Random(33).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        var options = new PipelineOptions
        {
            WorkerCount = 4,
            AffinityCores = new[] { 0, 2, 4, 6 }
        };
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null, options))
        {
            pipe.Flow();
        }
        munmap((nint)code, 4096);

        Assert.Equal(data, File.ReadAllBytes(dst));

        File.Delete(src);
        File.Delete(dst);
    }

    [Fact]
    public void Throws_When_AffinityCores_Shorter_Than_Workers()
    {
        byte[] data = new byte[64 * 8];
        new Random(44).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        var options = new PipelineOptions
        {
            WorkerCount = 4,
            AffinityCores = new[] { 0, 2 }
        };
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null, options))
        {
            Assert.Throws<ArgumentException>(() => pipe.Flow());
        }
        munmap((nint)code, 4096);

        File.Delete(src);
        if (File.Exists(dst))
        {
            File.Delete(dst);
        }
    }
}
