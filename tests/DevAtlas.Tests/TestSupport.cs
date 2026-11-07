using System.Diagnostics;
using DevAtlas.Application;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace DevAtlas.Tests;

internal sealed class TestDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("devatlas-test-").FullName;
    public string Folder(string name) => Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;
    public string File(string name, string content = "{}")
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }
    public void Dispose()
    {
        // Only remove the unique test-owned directory. Delete links without traversing targets.
        if (!System.IO.Path.GetFileName(Path).StartsWith("devatlas-test-", StringComparison.Ordinal)) throw new InvalidOperationException();
        foreach (var file in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
            System.IO.File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(Path, recursive: true);
    }
    public async Task<string> Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = Path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-c", "core.hooksPath=" + System.IO.Path.Combine(Path, ".disabled-hooks"), "-c", "core.fsmonitor=false", "-c", "user.name=DevAtlas Test", "-c", "user.email=test@example.invalid" }.Concat(arguments)) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await stderr);
        return await stdout;
    }
}

internal sealed class TestLog : IAppLog
{
    public List<string> Messages { get; } = [];
    public void Write(string message, Exception? exception = null) { lock (Messages) Messages.Add(message); }
}
