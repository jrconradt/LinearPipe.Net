# LinearPipe.Net

[![CI](https://github.com/jrconradt/LinearPipe.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/jrconradt/LinearPipe.Net/actions/workflows/ci.yml) ![.NET](https://img.shields.io/badge/.NET-10-512BD4) ![Arch](https://img.shields.io/badge/Linux_x86--64-AVX--512F-orange) [![License](https://img.shields.io/badge/License-Apache_2.0-blue)](LICENSE)

A memory-mapped, chunk-disjoint linear data-plane pipeline. Source and sink are mmap'd; the chunk space is partitioned into disjoint spans across worker threads; each 64-byte chunk is loaded, run through a per-chunk SIMD transform, and written to the sink with a non-temporal store. Wait-freedom is structural — workers never share a chunk, so no locks or atomics are needed on the hot path.

`new LinearPipeline(sourcePath, sinkPath, transform, consts, options)` then `Flow()`. The transform is an unmanaged function pointer — `delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>` — called once per 64-byte chunk with (output, input, consts) pointers, so there is no managed dispatch on the hot path. `consts` points at baked constants the transform reads (may be null). `PipelineOptions` carries the worker count and per-worker core affinity — either a `AffinityBase`/`AffinityStride` arithmetic map or an explicit `AffinityCores` array for NUMA-local placement.

Requires Linux on x86-64 with AVX512F. The sink uses `vmovntdq` non-temporal stores and each worker issues its own `sfence` before exiting, so every store is globally visible by the time `Flow()` joins; the constructor throws `PlatformNotSupportedException` when AVX512F is absent, as there is no fallback store path. Worker placement and page population go through `libc` (`sched_setaffinity`, `madvise` with `MADV_POPULATE_READ`/`MADV_POPULATE_WRITE`).

## Benchmarking

`LinearPipe.Net.Bench` is a library harness with no entry point. `Benchmark<T> where T : unmanaged, IBenchmarkSubject` runs `warmup` then `measured` iterations of `T.Run()`, timing each with `Tsc`: an `RDTSC`/`RDTSCP` bracket JIT-loaded into an `R|X` page and calibrated to nanoseconds against the invariant TSC. `BenchmarkResult` reports min/median/max nanoseconds. `PipelineSubject` benchmarks `LinearPipeline.Flow()` through a `GCHandle` bridge; `LinearPipelineBenchmarkTests.Benchmark_Flow_Reports_Nanoseconds` is a worked example. The harness is also Linux-x86-64-only (its `Tsc` stubs are raw `RDTSC` machine code).

## Build & test

```
dotnet build LinearPipe.Net.slnx
dotnet test LinearPipe.Net.slnx
```

## Status

Active development; API unstable. Used as the streaming substrate behind [FWHT.Net](https://github.com/jrconradt/FWHT.Net)'s benchmark. Linux on x86-64 with AVX-512F is required — there is no fallback store path.

## License

Apache-2.0. Copyright 2026 Infalligence Labs LLC — see [LICENSE](LICENSE).
