using LinearPipe;
using LinearPipe.Bench;
using Xunit;
using Xunit.Abstractions;

namespace LinearPipeTests;

public unsafe partial class LinearPipelineTests
{
    private readonly ITestOutputHelper _output;

    public LinearPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Benchmark_Flow_Reports_Nanoseconds()
    {
        byte[] data = new byte[64 * (1 << 16)];
        new Random(1).NextBytes(data);
        string src = WriteSource(data);
        string dst = src + ".out";

        byte* code = MapCode(IdentityCode);
        var fn = (delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>)code;
        using (LinearPipeline pipe = new LinearPipeline(src, dst, fn, null))
        {
            PipelineSubject subject = new PipelineSubject(pipe);
            Benchmark<PipelineSubject> bench = new Benchmark<PipelineSubject>(50, 500);
            BenchmarkResult result = bench.Run(subject);
            subject.Free();
            _output.WriteLine($"LinearPipeline.Flow over {data.Length >> 20} MiB (n={result.Iterations}): min={result.MinNanos:N0} ns, median={result.MedianNanos:N0} ns, max={result.MaxNanos:N0} ns");
            Assert.True(result.MedianNanos > 0);
        }
        munmap((nint)code, 4096);

        File.Delete(src);
        File.Delete(dst);
    }
}
