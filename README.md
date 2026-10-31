# LinearPipe.Net

[![CI](https://github.com/jrconradt/LinearPipe.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/jrconradt/LinearPipe.Net/actions/workflows/ci.yml) ![.NET](https://img.shields.io/badge/.NET-10-512BD4) ![Arch](https://img.shields.io/badge/Linux_x86--64-AVX--512F-orange) [![License](https://img.shields.io/badge/License-Apache_2.0-blue)](LICENSE)

A memory-mapped linear data-plane pipeline. You supply a source file, a worker count, a sink file and an operation; the pipeline streams the source through the operation one 64-byte `Vector512<ulong>` chunk at a time and writes each result to the sink with a non-temporal store.

```csharp
readonly struct Negate : IOperation
{
    public Vector512<ulong> Apply(Vector512<ulong> chunk) => ~chunk;
}

using Pipeline<Negate> pipeline = new Pipeline<Negate>(sourcePath, workers, sinkPath, new Negate());
await pipeline.Flow();
```

The operation is a struct type parameter, so `Apply` is inlined into the pipeline's chunk loop; constants it needs live in the struct's fields. Construct the pipeline once and flow as often as needed: construction maps both files, and the first `Flow` places the sink's pages.

Workers form one pool per process and run one `Flow` at a time. Worker `i` is pinned to core `i` and belongs to NUMA node `i % 2`, matching hosts that number cores alternately across two sockets. The source is split into one page-aligned region per node with workers, lower half to node 0; each node's region is dispatched only to that node's workers, and each node's half of the sink is populated from that node. Placing the source's own pages on the matching nodes is the caller's job. Work is handed out in 16 MiB blocks, so a source smaller than 16 MiB per node does not occupy every worker.

Every store is globally visible by the time `Flow` completes. Requires Linux on x86-64 with AVX-512F; there is no fallback path and no startup check, so on a host without AVX-512F the first store throws `PlatformNotSupportedException` on a worker thread, which ends the process.

## Benchmarking

`LinearPipe.Net.Bench` is a timing harness with no entry point. `Benchmark<T> where T : unmanaged, IBenchmarkSubject` runs `warmup` then `measured` iterations of `T.Run()`, times each with an `RDTSC`/`RDTSCP` bracket calibrated to nanoseconds, and reports min/median/max. `PipelineSubject<TOp>` wraps a `Pipeline<TOp>`; `PipelineBenchmarkTests.Benchmark_Flow_Reports_Nanoseconds` is a worked example.

## Build & test

```
dotnet build LinearPipe.Net.slnx
dotnet test LinearPipe.Net.slnx
```

## Status

Active development; API unstable.

## License

Apache-2.0. Copyright 2026 Infalligence Labs LLC — see [LICENSE](LICENSE).
