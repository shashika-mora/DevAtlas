using DevAtlas.Infrastructure;
using Xunit;

namespace DevAtlas.Tests;

public sealed class GitReaderTests
{
    [Fact]
    public async Task NonRepositoryDoesNotRequireGit()
    {
        using var root = new TestDirectory();
        var snapshot = await new ProcessGitReader(executablePath: "missing-git").ReadAsync(root.Path);
        Assert.False(snapshot.IsRepository);
    }

    [Fact]
    public async Task EmptyRepositoryHasBranchAndNoCommits()
    {
        using var root = new TestDirectory();
        await root.Git("init", "--initial-branch=main");
        var snapshot = await new ProcessGitReader().ReadAsync(root.Path);
        Assert.True(snapshot.IsRepository);
        Assert.Equal("main", snapshot.Branch);
        Assert.Empty(snapshot.RecentCommits);
    }

    [Fact]
    public async Task ReadsStatusRenamesUnicodeAndDetachedHead()
    {
        using var root = new TestDirectory();
        await root.Git("init", "--initial-branch=main");
        root.File("original.txt", "original\n");
        await root.Git("add", "."); await root.Git("commit", "-m", "initial");
        await root.Git("mv", "original.txt", "renamed file.txt");
        root.File("space ü.txt", "untracked");
        var snapshot = await new ProcessGitReader().ReadAsync(root.Path);
        Assert.Equal(2, snapshot.ChangedFileCount);
        Assert.Contains(snapshot.ChangedFiles, path => path.Contains("renamed file.txt") && path.Contains("original.txt"));
        Assert.Contains("space ü.txt", snapshot.ChangedFiles);
        Assert.Contains(snapshot.RecentCommits, commit => commit.EndsWith("initial", StringComparison.Ordinal));
        await root.Git("checkout", "--detach");
        Assert.Equal("(detached HEAD)", (await new ProcessGitReader().ReadAsync(root.Path)).Branch);
    }

    [Fact]
    public async Task RepositoryHelpersAreDisabled()
    {
        using var root = new TestDirectory();
        await root.Git("init", "--initial-branch=main");
        root.File("tracked.txt", "initial");
        await root.Git("add", "."); await root.Git("commit", "-m", "initial");
        // Harmless marker commands provide evidence that configured helpers never run.
        var marker = System.IO.Path.Combine(root.Path, "helper-ran.txt").Replace('\\', '/');
        var helper = $"echo helper > '{marker}'";
        await root.Git("config", "core.fsmonitor", helper);
        await root.Git("config", "filter.custom.clean", helper);
        await root.Git("config", "filter.custom.process", helper);
        await root.Git("config", "filter.custom.required", "true");
        root.File(".gitattributes", "*.txt filter=custom\n");
        root.File("tracked.txt", "modified content");
        var snapshot = await new ProcessGitReader().ReadAsync(root.Path);
        Assert.True(snapshot.ChangedFileCount > 0);
        Assert.False(System.IO.File.Exists(System.IO.Path.Combine(root.Path, "helper-ran.txt")));
    }

    [Fact]
    public async Task InheritedGitDirectoryCannotRedirectInspection()
    {
        using var root = new TestDirectory(); using var other = new TestDirectory();
        await root.Git("init", "--initial-branch=expected"); await other.Git("init", "--initial-branch=wrong");
        var prior = Environment.GetEnvironmentVariable("GIT_DIR");
        try
        {
            Environment.SetEnvironmentVariable("GIT_DIR", System.IO.Path.Combine(other.Path, ".git"));
            Assert.Equal("expected", (await new ProcessGitReader().ReadAsync(root.Path)).Branch);
        }
        finally { Environment.SetEnvironmentVariable("GIT_DIR", prior); }
    }

    [Fact]
    public async Task LinkedWorktreesAreInspected()
    {
        using var root = new TestDirectory(); using var worktree = new TestDirectory();
        await root.Git("init", "--initial-branch=main"); await root.Git("commit", "--allow-empty", "-m", "initial");
        var path = System.IO.Path.Combine(worktree.Path, "linked");
        await root.Git("worktree", "add", "-b", "linked", path);
        Assert.Equal("linked", (await new ProcessGitReader().ReadAsync(path)).Branch);
    }

    [Fact]
    public async Task CancelAndDeadlineAreReportedWithoutLeakingTemporaryConfiguration()
    {
        using var root = new TestDirectory(); await root.Git("init");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessGitReader().ReadAsync(root.Path, cancellation.Token));
        await Assert.ThrowsAsync<TimeoutException>(() => new ProcessGitReader(TimeSpan.Zero).ReadAsync(root.Path));
    }
}
