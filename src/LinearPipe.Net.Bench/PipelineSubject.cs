using System.Runtime.InteropServices;

namespace LinearPipe.Bench;

public readonly struct PipelineSubject<TOp> : IBenchmarkSubject
    where TOp : struct, IOperation
{
    private readonly GCHandle _pipeline;

    public PipelineSubject(Pipeline<TOp> pipeline)
    {
        _pipeline = GCHandle.Alloc(pipeline);
    }

    public void Run()
    {
        ((Pipeline<TOp>)_pipeline.Target!).Flow().GetAwaiter().GetResult();
    }

    public void Free()
    {
        _pipeline.Free();
    }
}
