using DevAtlas.Domain;
using DevAtlas.Infrastructure;
using Xunit;

namespace DevAtlas.Tests;

public sealed class ProjectDiscoveryTests
{
    [Fact]
    public async Task DiscoversGitProjectWithoutScanningIgnoredDirectories()
    {
        var root = Directory.CreateTempSubdirectory("devatlas-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "repo", ".git"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "repo", "node_modules", "nested"));
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "repo", "package.json"), "{}");

            var discovery = new FileSystemProjectDiscovery();
            var projects = new List<Project>();
            await foreach (var project in discovery.DiscoverAsync([new WorkspaceRoot(Guid.NewGuid(), root.FullName)]))
                projects.Add(project);

            var project = Assert.Single(projects);
            Assert.Equal("Node.js", project.ProjectType);
            Assert.Equal("JavaScript/TypeScript", project.Language);
        }
        finally { root.Delete(true); }
    }
}
