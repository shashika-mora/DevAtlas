using DevAtlas.Domain;
using DevAtlas.Infrastructure;
using Xunit;

namespace DevAtlas.Tests;

public sealed class ProjectDiscoveryTests
{
    [Theory]
    [InlineData("package.json", "Node.js")]
    [InlineData("sample.csproj", ".NET")]
    [InlineData("sample.slnx", ".NET")]
    [InlineData("sample.fsproj", ".NET")]
    [InlineData("compose.yaml", "Docker Compose")]
    [InlineData("Cargo.toml", "Rust")]
    [InlineData("pyproject.toml", "Python")]
    public async Task DetectsTechnologies(string marker, string expected)
    {
        using var root = new TestDirectory();
        root.File("project/" + marker);
        var result = await new FileSystemProjectDiscovery().DiscoverAsync([new WorkspaceRoot(Guid.NewGuid(), root.Path)]);
        Assert.Equal(expected, Assert.Single(result.Projects).ProjectType);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task DiscoversGitDirectoryAndPreservesTechnologyForGitFiles()
    {
        using var root = new TestDirectory();
        root.Folder("git-only/.git");
        root.File("node/.git", "gitdir: ../metadata");
        root.File("node/package.json");
        var result = await new FileSystemProjectDiscovery().DiscoverAsync([new WorkspaceRoot(Guid.NewGuid(), root.Path)]);
        Assert.Equal(2, result.Projects.Count);
        Assert.Contains(result.Projects, p => p.Name == "git-only" && p.ProjectType == "Git repository");
        Assert.Contains(result.Projects, p => p.Name == "node" && p.ProjectType == "Node.js");
    }

    [Theory]
    [InlineData("node_modules")]
    [InlineData(".git")]
    [InlineData(".venv")]
    [InlineData("bin")]
    [InlineData("obj")]
    public async Task DoesNotDiscoverMarkersInsideExcludedFolders(string excluded)
    {
        using var root = new TestDirectory();
        root.File(excluded + "/nested/package.json");
        var result = await new FileSystemProjectDiscovery().DiscoverAsync([new WorkspaceRoot(Guid.NewGuid(), root.Path)]);
        Assert.DoesNotContain(result.Projects, p => p.Name == "nested");
    }

    [Fact]
    public async Task OverlappingRootsAreDeduplicatedAndIdentitiesAreStable()
    {
        using var root = new TestDirectory();
        root.File("project/package.json");
        var discovery = new FileSystemProjectDiscovery();
        var roots = new[] { new WorkspaceRoot(Guid.NewGuid(), root.Path), new WorkspaceRoot(Guid.NewGuid(), System.IO.Path.Combine(root.Path, "project")) };
        var first = Assert.Single((await discovery.DiscoverAsync(roots)).Projects);
        var second = Assert.Single((await discovery.DiscoverAsync(roots)).Projects);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task IgnoredRootsAndUnavailableRootsDoNotBecomeCompletedRoots()
    {
        using var root = new TestDirectory();
        root.File("package.json");
        var result = await new FileSystemProjectDiscovery().DiscoverAsync([
            new WorkspaceRoot(Guid.NewGuid(), root.Path, true),
            new WorkspaceRoot(Guid.NewGuid(), System.IO.Path.Combine(root.Path, "missing"))]);
        Assert.Empty(result.Projects);
        Assert.Empty(result.CompletedRoots);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task CancellationIsObservedBeforeTraversal()
    {
        using var root = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileSystemProjectDiscovery().DiscoverAsync(
            [new WorkspaceRoot(Guid.NewGuid(), root.Path)], cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task DirectoryLinksAreScanBoundaries()
    {
        using var root = new TestDirectory();
        using var outside = new TestDirectory();
        outside.File("package.json");
        var link = System.IO.Path.Combine(root.Path, "linked");
        if (OperatingSystem.IsWindows())
        {
            var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "/c", "mklink", "/J", link, outside.Path }) info.ArgumentList.Add(arg);
            using var process = System.Diagnostics.Process.Start(info)!;
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, outside.Path);
        try
        {
            var result = await new FileSystemProjectDiscovery().DiscoverAsync([new WorkspaceRoot(Guid.NewGuid(), root.Path)]);
            Assert.Empty(result.Projects);
            var linkedRoot = await new FileSystemProjectDiscovery().DiscoverAsync([new WorkspaceRoot(Guid.NewGuid(), link)]);
            Assert.Empty(linkedRoot.CompletedRoots);
        }
        finally { Directory.Delete(link); }
    }
}
