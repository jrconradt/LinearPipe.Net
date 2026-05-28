using System.IO.MemoryMappedFiles;

namespace LinearPipe;

public sealed unsafe class MappedFile : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _view;
    private byte* _base;
    private readonly long _length;

    public byte* Base => _base;
    public long Length => _length;

    public static MappedFile Open(string path)
    {
        var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        byte* basePtr = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
        return new MappedFile(mmf, view, basePtr, view.Capacity);
    }

    public static MappedFile Create(string path, long capacity)
    {
        var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Create, null, capacity, MemoryMappedFileAccess.ReadWrite);
        var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        byte* basePtr = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);
        return new MappedFile(mmf, view, basePtr, view.Capacity);
    }

    private MappedFile(MemoryMappedFile mmf, MemoryMappedViewAccessor view, byte* basePtr, long length)
    {
        _mmf = mmf;
        _view = view;
        _base = basePtr;
        _length = length;
    }

    public void Dispose()
    {
        if (_base != null)
        {
            _view.SafeMemoryMappedViewHandle.ReleasePointer();
            _base = null;
        }
        _view.Dispose();
        _mmf.Dispose();
    }
}
