# LinearPipe.Net

A memory-mapped, chunk-disjoint linear data-plane pipeline. Source and sink are mmap'd; the chunk space is partitioned into disjoint spans across worker threads; each 64-byte chunk is loaded, run through a per-chunk SIMD transform, and written to the sink with a non-temporal store. Wait-freedom is structural — workers never share a chunk, so no locks or atomics are needed on the hot path.

`new LinearPipeline(sourcePath, sinkPath, transform, consts, options)` then `Flow()`. The transform is an unmanaged function pointer — `delegate* unmanaged[Cdecl, SuppressGCTransition]<ulong*, byte*, byte*, void>` — called once per 64-byte chunk with (output, input, consts) pointers, so there is no managed dispatch on the hot path. `consts` points at baked constants the transform reads (may be null). `PipelineOptions` carries the worker count and per-worker core affinity — either a `AffinityBase`/`AffinityStride` arithmetic map or an explicit `AffinityCores` array for NUMA-local placement.

This is the data-plane pipeline of `xfer-sharp` extracted into standalone, plain data-pipeline vocabulary, decoupled from any spectral substrate. It requires AVX512F: the sink uses `vmovntdq` non-temporal stores and each worker issues its own `sfence` before exiting, so every store is globally visible by the time `Flow()` joins.

## Build & test

```
dotnet build LinearPipe.Net.slnx
dotnet test LinearPipe.Net.slnx
```
