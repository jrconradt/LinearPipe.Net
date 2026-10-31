namespace LinearPipe;

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

internal interface IRunner
{
    void Run(in Block block);
}

internal sealed unsafe class Runner<TOp>(TOp operation) : IRunner
    where TOp : struct, IOperation
{
    private readonly TOp _operation = operation;

    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveOptimization)]
    public void Run(in Block block)
    {
        TOp op = _operation;
        Vector512<ulong>* src = block.Source;
        Vector512<ulong>* dst = block.Sink;
        nuint chunks = block.Chunks;
        nuint mainEnd = chunks & ~(nuint)3;

        for (nuint i = 0; i < mainEnd; i += 4)
        {
            Vector512<ulong> o0 = op.Apply(chunk: src[i + 0]);
            Vector512<ulong> o1 = op.Apply(chunk: src[i + 1]);
            Vector512<ulong> o2 = op.Apply(chunk: src[i + 2]);
            Vector512<ulong> o3 = op.Apply(chunk: src[i + 3]);
            Avx512F.StoreAlignedNonTemporal(address: (ulong*)(dst + i + 0), source: o0);
            Avx512F.StoreAlignedNonTemporal(address: (ulong*)(dst + i + 1), source: o1);
            Avx512F.StoreAlignedNonTemporal(address: (ulong*)(dst + i + 2), source: o2);
            Avx512F.StoreAlignedNonTemporal(address: (ulong*)(dst + i + 3), source: o3);
        }

        for (nuint j = mainEnd; j < chunks; j++)
        {
            Avx512F.StoreAlignedNonTemporal(address: (ulong*)(dst + j), source: op.Apply(chunk: src[j]));
        }

        Sse.StoreFence();
    }
}
