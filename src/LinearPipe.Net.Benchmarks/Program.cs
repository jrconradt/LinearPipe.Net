using System.Runtime.CompilerServices;
using LinearPipe.Benchmarks;

const int WARMUP = 100_000;
const int MEASURED = 1_000_000;

Benchmark<EmptySubject> bench = new Benchmark<EmptySubject>(WARMUP, MEASURED);
BenchmarkResult result = bench.Run(default);
Console.WriteLine($"Begin/End self-overhead (TSC ticks): min={result.MinTicks} median={result.MedianTicks} max={result.MaxTicks} n={result.Iterations}");

internal readonly struct EmptySubject : IBenchmarkSubject
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Run()
    {
    }
}
