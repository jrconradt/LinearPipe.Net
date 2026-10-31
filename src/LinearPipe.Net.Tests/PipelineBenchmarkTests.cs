namespace LinearPipeTests;

using LinearPipe;
using LinearPipe.Bench;
using Xunit;
using Xunit.Abstractions;

public partial class PipelineTests
{
    private readonly ITestOutputHelper _output;

    public PipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Benchmark_Flow_Reports_Nanoseconds()
    {
        byte[] data = Random(length: 64 * (1 << 16), seed: 1);
        string src = WriteSource(data: data);
        string dst = src + ".out";
        using (Pipeline<Identity> pipeline = new Pipeline<Identity>(sourcePath: src, workers: 10, sinkPath: dst, operation: new Identity()))
        {
            PipelineSubject<Identity> subject = new PipelineSubject<Identity>(pipeline: pipeline);
            BenchmarkResult result = new Benchmark<PipelineSubject<Identity>>(warmup: 50, measured: 500).Run(subject: subject);
            subject.Free();
            _output.WriteLine($"Pipeline.Flow over {data.Length >> 20} MiB (n={result.Iterations}): min={result.MinNanos:N0} ns, median={result.MedianNanos:N0} ns, max={result.MaxNanos:N0} ns");
            Assert.True(result.MedianNanos > 0);
        }

        File.Delete(path: src);
        File.Delete(path: dst);
    }
}
