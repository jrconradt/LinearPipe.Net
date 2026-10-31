namespace LinearPipe;

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading.Channels;

internal sealed class Foreman : IDisposable
{
    private const nuint BLOCK_BYTES = 16 * 1024 * 1024;
    private const nuint BLOCK_CHUNKS = BLOCK_BYTES / Block.CHUNK_BYTES;
    private const int SINK_MODE = 420;
    private const nuint PAGE_CHUNKS = 4096 / Block.CHUNK_BYTES;

    internal const int NUMA_NODES = 2;

    private static readonly Worker?[] _staff = new Worker?[Worker.CPUS];
    private static readonly Channel<Worker>[] _idle = new Channel<Worker>[NUMA_NODES];
    private static readonly int[] _hired = new int[NUMA_NODES];
    private static int _hiredTotal;

    private readonly unsafe byte* _source;
    private readonly unsafe byte* _sink;
    private readonly nuint _length;
    private readonly nuint _chunks;
    private readonly Region[] _regions = new Region[NUMA_NODES];
    private int _placedFor;

    [StructLayout(layoutKind: LayoutKind.Sequential, Size = 128)]
    private struct Region
    {
        public nuint Cursor;
        public nuint End;
    }

    static Foreman()
    {
        for (int node = 0; node < NUMA_NODES; node++)
        {
            _idle[node] = Channel.CreateUnbounded<Worker>(options: new UnboundedChannelOptions { SingleReader = true });
        }
    }

    public unsafe Foreman(string sourcePath,
                   string sinkPath)
    {
        int sourceFd = Native.open(path: sourcePath,
                                   flags: Native.O_RDONLY,
                                   mode: 0);
        _length = (nuint)Native.lseek(fd: sourceFd,
                                      offset: 0,
                                      whence: Native.SEEK_END);
        _chunks = (_length + Block.CHUNK_BYTES - 1) / Block.CHUNK_BYTES;

        int sinkFd = Native.open(path: sinkPath,
                                 flags: Native.O_RDWR | Native.O_CREAT | Native.O_TRUNC,
                                 mode: SINK_MODE);
        _ = Native.ftruncate(fd: sinkFd,
                         length: (long)_length);

        _source = (byte*)Native.mmap(addr: 0,
                                     length: _length,
                                     prot: Native.PROT_READ,
                                     flags: Native.MAP_SHARED,
                                     fd: sourceFd,
                                     offset: 0);
        _sink = (byte*)Native.mmap(addr: 0,
                                   length: _length,
                                   prot: Native.PROT_READ | Native.PROT_WRITE,
                                   flags: Native.MAP_SHARED,
                                   fd: sinkFd,
                                   offset: 0);
        _ = Native.close(fd: sourceFd);
        _ = Native.close(fd: sinkFd);

        _ = Native.madvise(addr: (nint)_source,
                       length: _length,
                       advice: Native.MADV_POPULATE_READ);
    }

    public static void Hire(int workers)
    {
        for (int node = 0; node < NUMA_NODES; node++)
        {
            while (_idle[node].Reader.TryRead(item: out _))
            {
            }
            _hired[node] = 0;
        }
        for (int core = 0; core < workers; core++)
        {
            Worker worker = _staff[core] ??= new Worker(core: core);
            _ = _idle[worker.Node].Writer.TryWrite(item: worker);
            _hired[worker.Node]++;
        }
        _hiredTotal = workers;
    }

    internal static void Release(Worker worker)
    {
        _ = _idle[worker.Node].Writer.TryWrite(item: worker);
    }

    public static async Task Drain(int node)
    {
        Worker[] returned = new Worker[_hired[node]];
        for (int i = 0; i < returned.Length; i++)
        {
            returned[i] = await _idle[node].Reader.ReadAsync();
        }
        foreach (Worker worker in returned)
        {
            _ = _idle[node].Writer.TryWrite(item: worker);
        }
    }

    public ValueTask<Worker?> Lease(int node)
    {
        if (_regions[node].Cursor == _regions[node].End)
        {
            return ValueTask.FromResult<Worker?>(result: null);
        }
        return _idle[node].Reader.ReadAsync()!;
    }

    public unsafe Block GetWork(int node)
    {
        ref Region region = ref _regions[node];
        nuint take = Math.Min(val1: region.End - region.Cursor,
                              val2: BLOCK_CHUNKS);
        nuint offset = region.Cursor * Block.CHUNK_BYTES;
        region.Cursor += take;
        return new Block(source: (Vector512<ulong>*)(_source + offset),
                         sink: (Vector512<ulong>*)(_sink + offset),
                         chunks: take);
    }

    public void Rewind()
    {
        int active = Math.Min(val1: _hiredTotal,
                              val2: NUMA_NODES);
        for (int node = 0; node < NUMA_NODES; node++)
        {
            _regions[node].Cursor = Boundary(index: node,
                                             active: active);
            _regions[node].End = Boundary(index: node + 1,
                                          active: active);
        }
        if (active != _placedFor)
        {
            PlaceSink();
            _placedFor = active;
        }
    }

    private unsafe void PlaceSink()
    {
        Thread[] placers = new Thread[NUMA_NODES];
        for (int node = 0; node < NUMA_NODES; node++)
        {
            int core = node;
            nint start = (nint)(_sink + (_regions[node].Cursor * Block.CHUNK_BYTES));
            nuint length = (_regions[node].End - _regions[node].Cursor) * Block.CHUNK_BYTES;
            placers[node] = new Thread(start: () =>
            {
                Worker.Pin(core: core);
                _ = Native.madvise(addr: start,
                                   length: length,
                                   advice: Native.MADV_POPULATE_WRITE);
            });
            placers[node].Start();
        }
        foreach (Thread placer in placers)
        {
            placer.Join();
        }
    }

    private nuint Boundary(int index,
                           int active)
    {
        if (index >= active)
        {
            return _chunks;
        }
        return (_chunks * (nuint)index / (nuint)active) & ~(PAGE_CHUNKS - 1);
    }

    public unsafe void Dispose()
    {
        _ = Native.munmap(addr: (nint)_sink,
                      length: _length);
        _ = Native.munmap(addr: (nint)_source,
                      length: _length);
    }
}
