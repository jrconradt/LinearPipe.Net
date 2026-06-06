namespace LinearPipe;

public interface IBenchmarkSubject
{
    void Run();
}

public sealed class Benchmark<T> where T : IBenchmarkSubject
{
    private readonly int _warmup;
    private readonly int _measured;

    public Benchmark(int warmup, int measured)
    {
        _warmup = warmup;
        _measured = measured;
    }

    public BenchmarkResult Run(T subject)
    {
        for (int i = 0; i < _warmup; i++)
        {
            subject.Run();
        }

        long[] samples = new long[_measured];
        for (int i = 0; i < _measured; i++)
        {
            ulong start = Tsc.Begin();
            subject.Run();
            ulong end = Tsc.End();
            samples[i] = (long)(end - start);
        }

        return BenchmarkResult.FromSamples(samples);
    }
}

public readonly struct BenchmarkResult
{
    public readonly int Iterations;
    public readonly long MinTicks;
    public readonly long MedianTicks;
    public readonly long MaxTicks;

    public BenchmarkResult(int iterations,
                           long minTicks,
                           long medianTicks,
                           long maxTicks)
    {
        Iterations = iterations;
        MinTicks = minTicks;
        MedianTicks = medianTicks;
        MaxTicks = maxTicks;
    }

    public static BenchmarkResult FromSamples(long[] samples)
    {
        Array.Sort(samples);
        int n = samples.Length;
        return new BenchmarkResult(n,
                                   samples[0],
                                   samples[n / 2],
                                   samples[n - 1]);
    }
}
