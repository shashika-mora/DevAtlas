using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using DevAtlas.Application;
using DevAtlas.Domain;

namespace DevAtlas.Infrastructure;

public sealed class ProcessGitReader(TimeSpan? timeout = null, string? executablePath = null) : IGitReader
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(20);
    private const int OutputLimit = 4 * 1024 * 1024;

    public async Task<GitSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(Path.Combine(path, ".git")) && !File.Exists(Path.Combine(path, ".git")))
            return new GitSnapshot(false, null, 0, [], []);
        var executable = executablePath ?? FindGit();
        var temporary = Directory.CreateTempSubdirectory("devatlas-git-");
        try
        {
            var config = Path.Combine(temporary.FullName, "empty.config");
            await File.WriteAllTextAsync(config, "", cancellationToken);
            var overrides = new List<string>
            {
                $"core.hooksPath={temporary.FullName}", "core.fsmonitor=false", "core.untrackedCache=false",
                "core.pager=", "color.ui=false", "diff.external=", "diff.ignoreSubmodules=all", "submodule.recurse=false",
                "log.showSignature=false", "maintenance.auto=false", "gc.auto=0", "protocol.allow=never", $"core.attributesFile={config}"
            };
            // Read names only. Override every configured filter, including arbitrary names and included config.
            var filters = await RunAsync(["config", "--null", "--name-only", "--get-regexp", @"^filter\..*\.(clean|smudge|process|required)$"], allowOne: true);
            foreach (var key in filters.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
                overrides.Add(key + (key.EndsWith(".required", StringComparison.OrdinalIgnoreCase) ? "=false" : "="));
            var branch = await RunAsync(["branch", "--show-current"]);
            var status = await RunAsync(["status", "--porcelain=v1", "-z", "--untracked-files=all", "--ignore-submodules=all"]);
            var head = await RunAsync(["rev-parse", "--verify", "--quiet", "HEAD"], allowOne: true);
            var commits = head.ExitCode == 0 ? (await RunAsync(["log", "-5", "--pretty=format:%h %s", "--no-ext-diff", "--no-show-signature"])).Output : "";
            var files = ParseStatus(status.Output);
            return new GitSnapshot(true, string.IsNullOrWhiteSpace(branch.Output) ? "(detached HEAD)" : branch.Output.Trim(),
                files.Count, files, commits.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

            async Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, bool allowOne = false)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(_timeout);
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        WorkingDirectory = path,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                var environment = process.StartInfo.Environment;
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "PATH", "SystemRoot", "WINDIR", "TEMP", "TMP", "HOME", "USERPROFILE", "LOCALAPPDATA",
                    "APPDATA", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432", "SystemDrive", "COMSPEC", "PATHEXT", "LANG", "LC_ALL"
                };
                foreach (var key in environment.Keys.ToArray()) if (!allowed.Contains(key)) environment.Remove(key);
                environment["GIT_CONFIG_NOSYSTEM"] = "1";
                environment["GIT_CONFIG_GLOBAL"] = config;
                environment["GIT_TERMINAL_PROMPT"] = "0";
                environment["GIT_OPTIONAL_LOCKS"] = "0";
                environment["GIT_NO_LAZY_FETCH"] = "1";
                environment["GIT_PAGER"] = "";
                environment["LC_ALL"] = "C.UTF-8";
                process.StartInfo.ArgumentList.Add("--no-optional-locks");
                foreach (var item in overrides)
                {
                    process.StartInfo.ArgumentList.Add("-c");
                    process.StartInfo.ArgumentList.Add(item);
                }
                foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
                try
                {
                    process.Start();
                    var output = ReadBoundedAsync(process.StandardOutput, process, deadline.Token);
                    var error = ReadBoundedAsync(process.StandardError, process, deadline.Token);
                    await Task.WhenAll(output, error, process.WaitForExitAsync(deadline.Token));
                    if (process.ExitCode != 0 && !(allowOne && process.ExitCode == 1))
                        throw new InvalidOperationException($"Git {arguments[0]} failed (exit code {process.ExitCode}).");
                    return (process.ExitCode, await output);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("Git inspection timed out.");
                }
                finally
                {
                    try
                    {
                        if (process.Id != 0 && !process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            await process.WaitForExitAsync();
                        }
                    }
                    catch (InvalidOperationException) { }
                    catch (Win32Exception) { }
                }
            }
        }
        finally
        {
            try { temporary.Delete(recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, Process process, CancellationToken token)
    {
        var output = new StringBuilder();
        var buffer = new char[8192];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (output.Length + count > OutputLimit)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
                throw new InvalidOperationException("Git output exceeded the inspection limit.");
            }
            output.Append(buffer, 0, count);
        }
        return output.ToString();
    }

    private static IReadOnlyList<string> ParseStatus(string status)
    {
        var entries = status.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var paths = new List<string>();
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 4) continue;
            var path = entry[3..];
            if ((entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') && i + 1 < entries.Length)
                path += " ← " + entries[++i];
            paths.Add(path);
        }
        return paths;
    }

    private static string FindGit()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            // Never resolve git from the scanned repository or a relative PATH entry.
            var candidateDirectory = directory.Trim('"');
            if (!Path.IsPathFullyQualified(candidateDirectory)) continue;
            var candidate = Path.Combine(candidateDirectory, OperatingSystem.IsWindows() ? "git.exe" : "git");
            if (File.Exists(candidate)) return candidate;
        }
        throw new Win32Exception("Git is unavailable. Install Git and add it to PATH.");
    }
}
