using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LinearPipe;

internal static partial class Libc
{
    public const int MadvPopulateRead = 22;
    public const int MadvPopulateWrite = 23;

    [LibraryImport("libc", EntryPoint = "madvise", SetLastError = true)]
    public static partial int Madvise(nint addr, nuint length, int advice);
}

public sealed unsafe class LinearPipeline : IDisposable
{
    private const ulong Page = 4096;

    public readonly string SourcePath;
    public readonly string SinkPath;

    private readonly delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void> _transform;
    private readonly byte* _consts;
    private readonly PipelineOptions _options;
    private MappedFile? _source;
    private MappedFile? _sink;
    private byte* _srcBase;
    private byte* _dstBase;
    private long _length;
    private bool _bound;

    private Thread[]? _pool;
    private Barrier? _gate;
    private volatile bool _shutdown;

    public LinearPipeline(
        string sourcePath,
        string sinkPath,
        delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void> transform,
        byte* consts,
        PipelineOptions? options = null)
    {
        if (!Avx512F.IsSupported)
        {
            throw new PlatformNotSupportedException("LinearPipeline requires AVX512F; the non-temporal sink store has no fallback path.");
        }
        SourcePath = sourcePath;
        SinkPath = sinkPath;
        _transform = transform;
        _consts = consts;
        _options = options ?? new PipelineOptions();
    }

    public void Flow()
    {
        Bind();
        if (_length == 0)
        {
            return;
        }
        if (_pool == null)
        {
            StartPool();
        }
        Barrier gate = _gate!;
        gate.SignalAndWait();
        gate.SignalAndWait();
    }

    private void StartPool()
    {
        ulong chunkCount = ((ulong)_length + 63) >> 6;
        int workers = _options.WorkerCount;
        if (workers < 1)
        {
            workers = 1;
        }
        if ((ulong)workers > chunkCount)
        {
            workers = (int)chunkCount;
        }

        if (_options.AffinityCores != null
            && _options.AffinityCores.Length < workers)
        {
            throw new ArgumentException($"PipelineOptions.AffinityCores has {_options.AffinityCores.Length} entries but {workers} workers each need one.");
        }

        byte* bp = _srcBase;
        byte* dst = _dstBase;
        byte* consts = _consts;
        var fn = _transform;
        int affinityBase = _options.AffinityBase;
        int affinityStride = _options.AffinityStride;
        int[]? cores = _options.AffinityCores;
        ulong baseSpan = chunkCount / (ulong)workers;
        ulong remainder = chunkCount % (ulong)workers;

        _gate = new Barrier(workers + 1);
        Barrier gate = _gate;
        _pool = new Thread[workers];
        for (int t = 0; t < workers; t++)
        {
            ulong lo = (ulong)t * baseSpan + Math.Min((ulong)t, remainder);
            ulong hi = lo + baseSpan + ((ulong)t < remainder ? 1UL : 0UL);
            int core = cores != null ? cores[t] : affinityBase + t * affinityStride;
            _pool[t] = new Thread(() =>
            {
                Affinity.PinCurrent(core);
                ulong pageStart = (lo << 6) & ~(Page - 1);
                ulong pageEnd = ((hi << 6) + Page - 1) & ~(Page - 1);
                nuint pageLen = (nuint)(pageEnd - pageStart);
                Libc.Madvise((nint)(dst + pageStart), pageLen, Libc.MadvPopulateWrite);
                Libc.Madvise((nint)(bp + pageStart), pageLen, Libc.MadvPopulateRead);
                while (true)
                {
                    gate.SignalAndWait();
                    if (_shutdown)
                    {
                        return;
                    }
                    ulong mainEnd = lo + ((hi - lo) & ~3UL);
                    Vector512<ulong> o0, o1, o2, o3;
                    for (ulong i = lo; i < mainEnd; i += 4)
                    {
                        fn((ulong*)&o0, bp + ((i + 0) << 6), consts);
                        fn((ulong*)&o1, bp + ((i + 1) << 6), consts);
                        fn((ulong*)&o2, bp + ((i + 2) << 6), consts);
                        fn((ulong*)&o3, bp + ((i + 3) << 6), consts);
                        Avx512F.StoreAlignedNonTemporal((ulong*)(dst + ((i + 0) << 6)), o0);
                        Avx512F.StoreAlignedNonTemporal((ulong*)(dst + ((i + 1) << 6)), o1);
                        Avx512F.StoreAlignedNonTemporal((ulong*)(dst + ((i + 2) << 6)), o2);
                        Avx512F.StoreAlignedNonTemporal((ulong*)(dst + ((i + 3) << 6)), o3);
                    }
                    for (ulong j = mainEnd; j < hi; j++)
                    {
                        ulong off = j << 6;
                        Vector512<ulong> output;
                        fn((ulong*)&output, bp + off, consts);
                        Avx512F.StoreAlignedNonTemporal((ulong*)(dst + off), output);
                    }
                    Sse.StoreFence();
                    gate.SignalAndWait();
                }
            })
            {
                IsBackground = true
            };
            _pool[t].Start();
        }
    }

    private void Bind()
    {
        if (_bound)
        {
            return;
        }
        _bound = true;
        _length = new FileInfo(SourcePath).Length;
        if (_length == 0)
        {
            File.WriteAllBytes(SinkPath, Array.Empty<byte>());
            return;
        }
        _source = MappedFile.Open(SourcePath);
        _sink = MappedFile.Create(SinkPath, _length);
        _srcBase = _source.Base;
        _dstBase = _sink.Base;
    }

    public void Dispose()
    {
        if (_pool != null)
        {
            _shutdown = true;
            _gate!.SignalAndWait();
            foreach (Thread thread in _pool)
            {
                thread.Join();
            }
            _gate.Dispose();
            _pool = null;
        }
        _sink?.Dispose();
        _source?.Dispose();
    }
}
