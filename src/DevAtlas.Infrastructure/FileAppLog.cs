using DevAtlas.Application;

namespace DevAtlas.Infrastructure;

public sealed class FileAppLog(string path) : IAppLog
{
    private readonly object _gate = new();
    public void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                    File.Move(path, path + ".previous", overwrite: true);
                // Exception messages and Git output can contain private repository data. Record only the type.
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{(exception == null ? "" : $" [{exception.GetType().Name}]")}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
