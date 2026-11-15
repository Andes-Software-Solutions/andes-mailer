namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// A directory of its own per test, removed with everything in it when the test ends.
public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Join(System.IO.Path.GetTempPath(), "andes-mailer-unit", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
