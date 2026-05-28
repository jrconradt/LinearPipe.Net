namespace LinearPipe;

public sealed record PipelineOptions
{
    public int WorkerCount { get; init; } = 10;
    public int AffinityBase { get; init; } = 0;
    public int AffinityStride { get; init; } = 2;
    public int[]? AffinityCores { get; init; }
}
