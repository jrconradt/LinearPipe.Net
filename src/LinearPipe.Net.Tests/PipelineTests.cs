namespace LinearPipeTests;

using System.Runtime.Intrinsics;
using LinearPipe;
using Xunit;

public partial class PipelineTests
{
    private readonly struct Identity : IOperation
    {
        public Vector512<ulong> Apply(Vector512<ulong> chunk)
        {
            return chunk;
        }
    }

    private readonly struct XorKey(byte key) : IOperation
    {
        private readonly Vector512<ulong> _key = Vector512.Create(value: key).AsUInt64();

        public Vector512<ulong> Apply(Vector512<ulong> chunk)
        {
            return chunk ^ _key;
        }
    }

    private static string WriteSource(byte[] data)
    {
        string path = Path.Combine(Path.GetTempPath(), $"linearpipe-src-{Guid.NewGuid():N}.dat");
        File.WriteAllBytes(path: path, bytes: data);
        return path;
    }

    private static byte[] Random(int length, int seed)
    {
        byte[] data = new byte[length];
        new Random(Seed: seed).NextBytes(buffer: data);
        return data;
    }

    private static byte[] Xor(byte[] data, byte key)
    {
        byte[] result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key);
        }
        return result;
    }

    private static byte[] Flow<TOp>(byte[] data, TOp operation, int workers, int flows)
        where TOp : struct, IOperation
    {
        string src = WriteSource(data: data);
        string dst = src + ".out";
        using (Pipeline<TOp> pipeline = new Pipeline<TOp>(sourcePath: src, workers: workers, sinkPath: dst, operation: operation))
        {
            for (int i = 0; i < flows; i++)
            {
                pipeline.Flow().GetAwaiter().GetResult();
            }
        }
        byte[] result = File.ReadAllBytes(path: dst);
        File.Delete(path: src);
        File.Delete(path: dst);
        return result;
    }

    [Fact]
    public void Identity_Reproduces_Source()
    {
        byte[] data = Random(length: 64 * 50, seed: 11);
        Assert.Equal(expected: data, actual: Flow(data: data, operation: new Identity(), workers: 4, flows: 1));
    }

    [Fact]
    public void Pool_Reused_Across_Flows()
    {
        byte[] data = Random(length: 64 * 40, seed: 5);
        Assert.Equal(expected: data, actual: Flow(data: data, operation: new Identity(), workers: 4, flows: 2));
    }

    [Fact]
    public void Xor_Transform_Applies_Per_Chunk()
    {
        byte[] data = Random(length: 64 * 37, seed: 99);
        Assert.Equal(expected: Xor(data: data, key: 0xA5), actual: Flow(data: data, operation: new XorKey(key: 0xA5), workers: 4, flows: 1));
    }

    [Fact]
    public void Partial_Tail_Chunk_Roundtrips()
    {
        byte[] data = Random(length: 64 * 5 + 13, seed: 7);
        Assert.Equal(expected: data, actual: Flow(data: data, operation: new Identity(), workers: 4, flows: 1));
    }

    [Fact]
    public void Partial_Tail_Chunk_Xor_Applies()
    {
        byte[] data = Random(length: 64 * 3 + 40, seed: 13);
        Assert.Equal(expected: Xor(data: data, key: 0x5A), actual: Flow(data: data, operation: new XorKey(key: 0x5A), workers: 4, flows: 1));
    }

    [Fact]
    public void Empty_Source_Produces_Empty_Sink()
    {
        Assert.Empty(collection: Flow(data: Array.Empty<byte>(), operation: new Identity(), workers: 4, flows: 1));
    }

    [Fact]
    public void Fewer_Chunks_Than_Workers_Roundtrips()
    {
        byte[] data = Random(length: 64 * 3, seed: 21);
        Assert.Equal(expected: data, actual: Flow(data: data, operation: new Identity(), workers: 10, flows: 1));
    }

    [Fact]
    public void Multiple_Blocks_Roundtrip_Across_Workers()
    {
        byte[] data = Random(length: 256 * 1024 * 3 + 4097, seed: 55);
        Assert.Equal(expected: Xor(data: data, key: 0x3C), actual: Flow(data: data, operation: new XorKey(key: 0x3C), workers: 4, flows: 2));
    }
}
