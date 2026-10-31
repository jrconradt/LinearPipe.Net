namespace LinearPipe;

using System.Runtime.Intrinsics;

internal readonly unsafe struct Block(Vector512<ulong>* source,
                                      Vector512<ulong>* sink,
                                      nuint chunks)
{
    public const nuint CHUNK_BYTES = 64;

    public readonly Vector512<ulong>* Source = source;
    public readonly Vector512<ulong>* Sink = sink;
    public readonly nuint Chunks = chunks;
}
