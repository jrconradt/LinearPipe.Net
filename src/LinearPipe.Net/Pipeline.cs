namespace LinearPipe;

public sealed class Pipeline<TOp> : IDisposable
    where TOp : struct, IOperation
{
    private readonly Foreman _foreman;
    private readonly int _workers;
    private readonly Runner<TOp> _runner;

    public Pipeline(string sourcePath,
                    int workers,
                    string sinkPath,
                    TOp operation)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value: workers,
                                                    other: 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value: workers,
                                                       other: Worker.CPUS);
        _foreman = new Foreman(sourcePath: sourcePath,
                               sinkPath: sinkPath);
        _workers = workers;
        _runner = new Runner<TOp>(operation: operation);
    }

    public async Task Flow()
    {
        Foreman.Hire(workers: _workers);
        _foreman.Rewind();
        Task[] dispatchers = new Task[Foreman.NUMA_NODES];
        for (int node = 0; node < Foreman.NUMA_NODES; node++)
        {
            dispatchers[node] = Dispatch(node: node);
        }
        await Task.WhenAll(tasks: dispatchers);
    }

    private async Task Dispatch(int node)
    {
        while (await _foreman.Lease(node: node) is Worker worker)
        {
            worker.Work(runner: _runner,
                        block: _foreman.GetWork(node: node));
        }
        await Foreman.Drain(node: node);
    }

    public void Dispose()
    {
        _foreman.Dispose();
    }
}
