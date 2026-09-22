using System.Diagnostics;
using DevAtlas.Application;
using DevAtlas.Domain;

namespace DevAtlas.Infrastructure;

public sealed class ProcessGitReader : IGitReader
{
    public async Task<GitSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(path, ".git")) && !File.Exists(Path.Combine(path, ".git")))
            return new GitSnapshot(false, null, 0, [], []);

        var branch = await RunAsync(path, ["branch", "--show-current"], cancellationToken);
        var status = await RunAsync(path, ["status", "--porcelain", "--no-optional-locks"], cancellationToken);
        var commits = await RunAsync(path, ["log", "-5", "--pretty=format:%h %s", "--no-ext-diff"], cancellationToken);
        var files = status.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Length > 3 ? line[3..].Trim() : line.Trim()).ToArray();
        return new GitSnapshot(true, branch.Trim(), files.Length, files, commits.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries));
    }

    private static async Task<string> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var configDirectory = Directory.CreateTempSubdirectory("devatlas-git-");
        var globalConfig = Path.Combine(configDirectory.FullName, "global.gitconfig");
        await File.WriteAllTextAsync(globalConfig, string.Empty, cancellationToken);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        try
        {
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add($"core.hooksPath={configDirectory.FullName}");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("core.fsmonitor=false");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("diff.external=");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("filter.lfs.process=");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("filter.lfs.clean=");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("filter.lfs.smudge=");
            process.StartInfo.ArgumentList.Add("-c");
            process.StartInfo.ArgumentList.Add("filter.lfs.required=false");
            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);
            process.StartInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            process.StartInfo.Environment["GIT_CONFIG_GLOBAL"] = globalConfig;
            process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Git {string.Join(' ', arguments)} failed: {error.Trim()}");
            return output;
        }
        finally
        {
            try
            {
                configDirectory.Delete(true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
