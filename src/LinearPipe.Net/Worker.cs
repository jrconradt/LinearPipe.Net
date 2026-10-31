namespace LinearPipe;

using System.Runtime.InteropServices;

internal sealed unsafe class Worker
{
    private const int CPU_SET_BYTES = 128;

    internal static readonly int CPUS = Environment.ProcessorCount;
    private static readonly byte* _masks;

    internal readonly int Node;

    private readonly SemaphoreSlim _handed = new SemaphoreSlim(initialCount: 0);
    private IRunner? _runner;
    private Block _block;

    static Worker()
    {
        _masks = (byte*)NativeMemory.AlignedAlloc(byteCount: (nuint)(CPUS * CPU_SET_BYTES),
                                                  alignment: 64);
        new Span<byte>(pointer: _masks, length: CPUS * CPU_SET_BYTES).Clear();
        for (int core = 0; core < CPUS; core++)
        {
            _masks[(core * CPU_SET_BYTES) + (core >> 3)] = (byte)(1 << (core & 7));
        }
    }

    internal Worker(int core)
    {
        Node = core % Foreman.NUMA_NODES;
        Thread thread = new Thread(start: () => Serve(core: core))
        {
            IsBackground = true,
            Name = $"LinearPipe worker @ core {core}"
        };
        thread.Start();
    }

    public void Work(IRunner runner,
                     in Block block)
    {
        _runner = runner;
        _block = block;
        _ = _handed.Release();
    }

    internal static void Pin(int core)
    {
        _ = Native.sched_setaffinity(pid: 0,
                                     cpusetsize: CPU_SET_BYTES,
                                     mask: _masks + (core * CPU_SET_BYTES));
    }

    private void Serve(int core)
    {
        Pin(core: core);
        while (true)
        {
            _handed.Wait();
            _runner!.Run(block: in _block);
            Foreman.Release(worker: this);
        }
    }
}
