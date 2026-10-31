namespace LinearPipe;

using System.Runtime.Intrinsics;

public interface IOperation
{
    Vector512<ulong> Apply(Vector512<ulong> chunk);
}
