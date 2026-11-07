using DevAtlas.App;
using DevAtlas.Application;
using DevAtlas.Domain;
using DevAtlas.Infrastructure;
using Xunit;

namespace DevAtlas.Tests;

public sealed class ViewModelTests
{
    private sealed class Git : IGitReader
    {
        public Task<GitSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(new GitSnapshot(false, null, 0, [], []));
    }
    private static MainWindowViewModel Create(SqliteWorkspaceStore store) => new(store, store,
        new WorkspaceScanner(store, store, new FileSystemProjectDiscovery(), new Git(), new TestLog()), new Git(), new TestLog());

    [Fact]
    public async Task StartupCountersSearchAndFavoritesReflectSavedState()
    {
        using var root = new TestDirectory();
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "app.db"));
        await store.SaveAsync(new Project(Guid.NewGuid(), "Atlas", root.Path, ".NET", "C#", ChangedFileCount: 3, IsFavorite: true));
        var vm = Create(store);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        await vm.InitializeAsync();
        Assert.Equal(1, vm.ModifiedCount); Assert.False(vm.HasNoProjects); Assert.Contains(nameof(vm.ModifiedCount), notifications);
        vm.SearchText = ".NET"; vm.FavoritesOnly = true; Assert.Single(vm.FilteredProjects);
        vm.SearchText = "no match"; Assert.Empty(vm.FilteredProjects); Assert.Equal(0, vm.VisibleCount);
        vm.Close();
    }

    [Fact]
    public async Task RootControlsPreserveDiskFilesAndFavoriteSurvivesRescan()
    {
        using var root = new TestDirectory();
        var marker = root.File("project/package.json");
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "app.db"));
        var vm = Create(store);
        await vm.InitializeAsync(); await vm.AddRootAsync(root.Path);
        vm.SelectedProject = Assert.Single(vm.Projects);
        var selectedPath = vm.SelectedProject.Path;
        await vm.ToggleFavoriteCommand.ExecuteAsync(null);
        Assert.Equal(selectedPath, vm.SelectedProject?.Path);
        Assert.True(vm.SelectedProject!.IsFavorite);
        await vm.RescanCommand.ExecuteAsync(null);
        Assert.True(Assert.Single(vm.Projects).IsFavorite);
        await vm.ToggleRootCommand.ExecuteAsync(Assert.Single(vm.Roots));
        Assert.Empty(vm.Projects); Assert.True(System.IO.File.Exists(marker));
        await vm.ToggleRootCommand.ExecuteAsync(Assert.Single(vm.Roots));
        await vm.RescanCommand.ExecuteAsync(null); Assert.True(Assert.Single(vm.Projects).IsFavorite);
        await vm.RemoveRootCommand.ExecuteAsync(Assert.Single(vm.Roots));
        Assert.Empty(vm.Roots); Assert.Empty(vm.Projects); Assert.True(System.IO.File.Exists(marker));
        Assert.Contains("Files on disk are unchanged", vm.StatusMessage);
        vm.Close();
    }

    [Fact]
    public async Task ErrorsRemainVisibleAndBusyStateRecovers()
    {
        using var root = new TestDirectory();
        var store = new SqliteWorkspaceStore(System.IO.Path.Combine(root.Path, "app.db"));
        var vm = Create(store);
        await vm.AddRootAsync(System.IO.Path.Combine(root.Path, "missing"));
        Assert.Contains("failed", vm.StatusMessage); Assert.False(vm.IsBusy); Assert.True(vm.CanEdit);
        vm.Close();
    }
}
