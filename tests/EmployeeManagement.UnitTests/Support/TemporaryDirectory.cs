namespace EmployeeManagement.UnitTests.Support;

// Every test owns one directory; no real profile uploads are read or removed.
public sealed class TemporaryDirectory : IDisposable
{
    private readonly string _parent = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Kreatx.UnitTests"));

    public string Path { get; }

    public TemporaryDirectory()
    {
        Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(_parent, Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        // Validate the absolute deletion target before recursively removing test files.
        if (System.IO.Path.GetDirectoryName(Path) != _parent ||
            !Guid.TryParseExact(System.IO.Path.GetFileName(Path), "N", out _))
            throw new InvalidOperationException("Refusing to remove a directory outside this test's temporary root.");
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
