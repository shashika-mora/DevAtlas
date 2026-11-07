using DevAtlas.Application;
using DevAtlas.Domain;
using DevAtlas.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DevAtlas.Tests;

public sealed class WorkspaceTests
{
    private sealed class NoGit : IGitReader
    {
        public bool Fail { get; set; }
        public Task<GitSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default) =>
            Fail ? Task.FromException<GitSnapshot>(new System.ComponentModel.Win32Exception()) : Task.FromResult(new GitSnapshot(false, null, 0, [], []));
    }

    [Fact]
    public async Task RescanPreservesIdentityFavoritesAndRealActivityAcrossRemovalAndReturn()
    {
        using var root = new TestDirectory();
        var marker = root.File("project/package.json");
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "workspace.db"));
        var workspaceRoot = new WorkspaceRoot(Guid.NewGuid(), root.Path);
        await store.AddAsync(workspaceRoot);
        await store.AddAsync(new WorkspaceRoot(Guid.NewGuid(), System.IO.Path.GetDirectoryName(marker)!));
        var scanner = new WorkspaceScanner(store, store, new FileSystemProjectDiscovery(), new NoGit(), new TestLog());
        var first = Assert.Single((await scanner.ScanAsync()).Projects);
        var activity = DateTimeOffset.UtcNow.AddDays(-2);
        await store.SaveAsync(first with { IsFavorite = true, LastWorkedAt = activity });
        var second = Assert.Single((await scanner.ScanAsync()).Projects);
        Assert.Equal(first.Id, second.Id);
        Assert.True(second.IsFavorite);
        Assert.Equal(activity, second.LastWorkedAt);
        Assert.NotNull(second.LastScannedAt);
        System.IO.File.Delete(marker);
        Assert.Empty((await scanner.ScanAsync()).Projects);
        var reopened = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "workspace.db"));
        Assert.Empty(await reopened.GetProjectsAsync());
        Assert.Single(await reopened.GetProjectsAsync(includeUnavailable: true));
        System.IO.File.WriteAllText(marker, "{}");
        var returned = Assert.Single((await scanner.ScanAsync()).Projects);
        Assert.Equal(first.Id, returned.Id);
        Assert.True(returned.IsFavorite);
    }

    [Fact]
    public async Task GitFailureStillPersistsProjectAndVisibleWarning()
    {
        using var root = new TestDirectory();
        root.File("package.json");
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "workspace.db"));
        await store.AddAsync(new WorkspaceRoot(Guid.NewGuid(), root.Path));
        var scanner = new WorkspaceScanner(store, store, new FileSystemProjectDiscovery(), new NoGit { Fail = true }, new TestLog());
        var result = await scanner.ScanAsync();
        Assert.NotNull(Assert.Single(result.Projects).GitError);
        Assert.Single(result.Warnings);
        Assert.Single(await store.GetProjectsAsync());
    }

    [Fact]
    public async Task IncompleteRootRetainsPreviousProjectsAndRemovingRootHidesThem()
    {
        using var root = new TestDirectory();
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "workspace.db"));
        var unavailable = new WorkspaceRoot(Guid.NewGuid(), System.IO.Path.Combine(root.Path, "offline"));
        await store.AddAsync(unavailable);
        var project = new Project(Guid.NewGuid(), "saved", System.IO.Path.Combine(unavailable.Path, "saved"), "Node.js", "JS", IsFavorite: true);
        await store.SaveAsync(project);
        var scanner = new WorkspaceScanner(store, store, new FileSystemProjectDiscovery(), new NoGit(), new TestLog());
        Assert.Single((await scanner.ScanAsync()).Projects);
        await store.SetIgnoredAsync(unavailable.Id, true);
        await store.ApplyScanAsync([], []);
        Assert.Empty(await store.GetProjectsAsync());
        Assert.True(Assert.Single(await store.GetProjectsAsync(includeUnavailable: true)).IsFavorite);
        await store.SetIgnoredAsync(unavailable.Id, false);
        await store.RemoveAsync(unavailable.Id);
        Assert.Empty((await scanner.ScanAsync()).Projects);
    }

    [Fact]
    public async Task CancelledScanDoesNotReplaceSavedState()
    {
        using var root = new TestDirectory();
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "workspace.db"));
        var project = new Project(Guid.NewGuid(), "saved", root.Path, "Node.js", "JS");
        await store.SaveAsync(project);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ApplyScanAsync([], [root.Path], cancellation.Token));
        Assert.Equal(project.Id, Assert.Single(await store.GetProjectsAsync()).Id);
    }

    [Fact]
    public async Task LegacyDatabaseMigratesWithoutLosingDataAndFutureSchemaIsRejected()
    {
        using var root = new TestDirectory();
        var database = System.IO.Path.Combine(root.Path, "legacy.db");
        var id = Guid.NewGuid();
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE projects (id TEXT PRIMARY KEY, name TEXT NOT NULL, path TEXT NOT NULL UNIQUE, project_type TEXT NOT NULL, language TEXT NOT NULL, current_branch TEXT, changed_file_count INTEGER NOT NULL DEFAULT 0, last_worked_at TEXT, is_favorite INTEGER NOT NULL DEFAULT 0); INSERT INTO projects VALUES ($id, 'legacy', $path, 'Node.js', 'JS', NULL, 0, NULL, 1)";
            command.Parameters.AddWithValue("$id", id.ToString()); command.Parameters.AddWithValue("$path", root.Path);
            command.ExecuteNonQuery();
        }
        var store = new SqliteWorkspaceStore(database);
        var project = Assert.Single(await store.GetProjectsAsync());
        Assert.Equal(id, project.Id); Assert.True(project.IsFavorite); Assert.Null(project.LastScannedAt);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString()))
        {
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version = 99"; command.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SqliteWorkspaceStore(database).GetProjectsAsync());
    }

    [Fact]
    public async Task DatabasePathsWithConnectionStringCharactersAndConcurrentOperationsWork()
    {
        using var root = new TestDirectory();
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "semi;colon.db"));
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => store.SaveAsync(new Project(Guid.NewGuid(), "p" + i, System.IO.Path.Combine(root.Path, "p" + i), "Node.js", "JS"))));
        Assert.Equal(12, (await store.GetProjectsAsync()).Count);
    }

    [Fact]
    public async Task LegacyTrailingSeparatorPathsPreserveIdentityDuringScan()
    {
        using var root = new TestDirectory();
        root.File("package.json");
        var database = System.IO.Path.Combine(root.Path, "legacy-alias.db");
        var id = Guid.NewGuid();
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE projects (id TEXT PRIMARY KEY, name TEXT NOT NULL, path TEXT NOT NULL UNIQUE, project_type TEXT NOT NULL, language TEXT NOT NULL, current_branch TEXT, changed_file_count INTEGER NOT NULL DEFAULT 0, last_worked_at TEXT, is_favorite INTEGER NOT NULL DEFAULT 0); INSERT INTO projects VALUES ($id, 'legacy', $path, 'Node.js', 'JS', NULL, 0, NULL, 1)";
            command.Parameters.AddWithValue("$id", id.ToString());
            command.Parameters.AddWithValue("$path", root.Path + System.IO.Path.DirectorySeparatorChar);
            command.ExecuteNonQuery();
        }
        var store = new SqliteWorkspaceStore(database);
        await store.AddAsync(new WorkspaceRoot(Guid.NewGuid(), root.Path));
        var scanner = new WorkspaceScanner(store, store, new FileSystemProjectDiscovery(), new NoGit(), new TestLog());
        var result = Assert.Single((await scanner.ScanAsync()).Projects);
        Assert.Equal(id, result.Id);
        Assert.True(result.IsFavorite);
        Assert.Equal(root.Path, result.Path);
    }

    private sealed class CancellableGit : IGitReader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<GitSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException();
        }
    }

    [Fact]
    public async Task CancellingDuringInspectionKeepsThePreviousSavedList()
    {
        using var root = new TestDirectory();
        root.File("package.json");
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "app.db"));
        await store.AddAsync(new WorkspaceRoot(Guid.NewGuid(), root.Path));
        var previous = new Project(Guid.NewGuid(), "previous", System.IO.Path.Combine(root.Path, "previous"), "Node.js", "JS", IsFavorite: true);
        await store.SaveAsync(previous);
        var git = new CancellableGit();
        var scanner = new WorkspaceScanner(store, store, new FileSystemProjectDiscovery(), git, new TestLog());
        using var cancellation = new CancellationTokenSource();
        var scan = scanner.ScanAsync(cancellationToken: cancellation.Token);
        await git.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan);
        Assert.Equal(previous, Assert.Single(await store.GetProjectsAsync()));
    }
}
